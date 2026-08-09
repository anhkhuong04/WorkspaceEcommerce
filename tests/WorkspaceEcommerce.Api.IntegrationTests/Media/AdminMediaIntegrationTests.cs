using System.Net;
using System.Net.Http.Headers;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Media;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Media;
using WorkspaceEcommerce.Infrastructure.Persistence;
using WorkspaceEcommerce.Domain.Modules.Media;

namespace WorkspaceEcommerce.Api.IntegrationTests.Media;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminMediaIntegrationTests(ApiIntegrationTestFixture fixture)
{
    // The API must re-encode this generated PNG rather than serve it verbatim.
    private static readonly byte[] ValidPng = CreateValidPng();

    [Fact]
    public async Task Upload_ValidImage_PersistsTrackedCanonicalAssetWithTrustedUrl()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var form = CreateImageForm(ValidPng, "banner.png", "image/png");

        using var response = await client.PostAsync("/api/admin/media", form);
        var json = await response.ReadJsonAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, json.ToJsonString());
        Assert.Equal("image/webp", json["data"]!["contentType"]!.GetValue<string>());
        Assert.StartsWith("http://localhost:5080/media/banners/", json["data"]!["url"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("banner.png", json["data"]!["objectKey"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var asset = await fixture.ExecuteDbAsync(async dbContext =>
            await Task.FromResult(dbContext.MediaAssets.Single()));
        Assert.Equal(MediaAssetState.Available, asset.State);
        Assert.Equal("image/webp", asset.ContentType);
        Assert.NotEmpty(asset.Checksum);
    }

    [Fact]
    public async Task Upload_SpoofedContentType_IsRejectedWithoutAssetRecord()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var form = CreateImageForm(ValidPng, "payload.jpg", "image/jpeg");

        using var response = await client.PostAsync("/api/admin/media", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var count = await fixture.ExecuteDbAsync(async dbContext =>
            await Task.FromResult(dbContext.MediaAssets.Count()));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Upload_WithoutAuthentication_IsRejected()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        using var form = CreateImageForm(ValidPng, "banner.png", "image/png");

        using var response = await client.PostAsync("/api/admin/media", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StorageFailure_LeavesOnlyAFailedNonUsableAssetRecord()
    {
        await fixture.ResetDatabaseAsync();

        var assetState = await fixture.ExecuteScopeAsync(async services =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var service = CreateDurableService(dbContext, new InMemoryObjectStore { ThrowOnPut = true });
            await using var stream = new MemoryStream(ValidPng);

            var result = await service.SaveAsync(new MediaUploadRequest(
                stream, "banner.png", "image/png", ValidPng.Length, "banners", "admin@example.test"));
            var asset = await dbContext.MediaAssets.SingleAsync();

            Assert.False(result.IsSuccess);
            return asset.State;
        });

        Assert.Equal(MediaAssetState.Failed, assetState);
    }

    [Fact]
    public async Task DeleteIfUnreferencedAsync_RetainsSharedAssetUntilLastReferenceIsRemoved()
    {
        await fixture.ResetDatabaseAsync();

        var deletedObjectCount = await fixture.ExecuteScopeAsync(async services =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var store = new InMemoryObjectStore();
            var service = CreateDurableService(dbContext, store);
            await using var stream = new MemoryStream(ValidPng);
            var upload = await service.SaveAsync(new MediaUploadRequest(
                stream, "banner.png", "image/png", ValidPng.Length, "banners", "admin@example.test"));
            Assert.True(upload.IsSuccess);

            var banner = new WorkspaceEcommerce.Domain.Modules.Content.Banner(
                Guid.NewGuid(), "Shared", upload.Value!.Url, null, 0);
            dbContext.Banners.Add(banner);
            await dbContext.SaveChangesAsync();

            var retained = await service.DeleteIfUnreferencedAsync(upload.Value.Url);
            Assert.True(retained.IsSuccess);
            Assert.Empty(store.DeletedKeys);

            dbContext.Banners.Remove(banner);
            await dbContext.SaveChangesAsync();
            var deleted = await service.DeleteIfUnreferencedAsync(upload.Value.Url);
            Assert.True(deleted.IsSuccess);

            return store.DeletedKeys.Count;
        });

        Assert.True(deletedObjectCount >= 1);
    }

    [Fact]
    public async Task AvailabilityPersistenceFailure_LeavesObservablePendingCleanupCandidate()
    {
        await fixture.ResetDatabaseAsync();

        var persistedState = await fixture.ExecuteScopeAsync(async services =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var options = CreateOptions();
            var service = new FailingAvailabilityMediaStorageService(
                dbContext,
                new InMemoryObjectStore(),
                new NoOpMediaMalwareScanner(),
                new MediaImageProcessor(options),
                options);
            await using var stream = new MemoryStream(ValidPng);

            var result = await service.SaveAsync(new MediaUploadRequest(
                stream, "banner.png", "image/png", ValidPng.Length, "banners", "admin@example.test"));
            var assetId = dbContext.MediaAssets.Local.Single().Id;
            dbContext.ChangeTracker.Clear();
            var asset = await dbContext.MediaAssets.AsNoTracking().SingleAsync(value => value.Id == assetId);

            Assert.False(result.IsSuccess);
            return asset.State;
        });

        Assert.Equal(MediaAssetState.Pending, persistedState);
    }

    private static MultipartFormDataContent CreateImageForm(byte[] bytes, string name, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", name);
        form.Add(new StringContent("banners"), "folder");
        return form;
    }

    private static byte[] CreateValidPng()
    {
        using var image = new MagickImage(MagickColors.Red, 32, 32);
        return image.ToByteArray(MagickFormat.Png);
    }

    private static DurableMediaStorageService CreateDurableService(AppDbContext dbContext, InMemoryObjectStore store)
    {
        var options = CreateOptions();
        return new DurableMediaStorageService(
            dbContext,
            store,
            new NoOpMediaMalwareScanner(),
            new MediaImageProcessor(options),
            options);
    }

    private static MediaStorageOptions CreateOptions() => new()
    {
        Provider = "Local",
        PublicBaseUrl = "https://assets.example.test",
        MaxUploadBytes = 5 * 1024 * 1024,
        MaxWidth = 4096,
        MaxHeight = 4096,
        MaxPixels = 16_000_000,
        CleanupRetentionHours = 24
    };

    private sealed class InMemoryObjectStore : IMediaObjectStore
    {
        public bool ThrowOnPut { get; init; }
        public HashSet<string> DeletedKeys { get; } = [];

        public Task PutAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
        {
            if (ThrowOnPut)
            {
                throw new IOException("Injected object-store failure.");
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FailingAvailabilityMediaStorageService(
        AppDbContext dbContext,
        IMediaObjectStore objectStore,
        IMediaMalwareScanner malwareScanner,
        MediaImageProcessor imageProcessor,
        MediaStorageOptions options)
        : DurableMediaStorageService(dbContext, objectStore, malwareScanner, imageProcessor, options)
    {
        protected override Task PersistAvailabilityAsync(CancellationToken cancellationToken) =>
            Task.FromException(new DbUpdateException("Injected database availability persistence failure."));
    }
}
