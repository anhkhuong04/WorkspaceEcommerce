using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Api.IntegrationTests.Shipments;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class ShippingRateAdminIntegrationTests(ApiIntegrationTestFixture fixture)
{
    private const string Root = "/api/admin/shipping/rate-plans";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DraftPreviewPublishCloneAndReplace_PreserveHistoryAndFrozenQuote()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var definition = await DefinitionAsync(client);
        var incomplete = Copy(definition, zones: [definition.Zones.Single() with { ProvinceCodes = ["01"] }]);
        var draft = await CreateAsync(client, incomplete);
        Assert.False(draft.Coverage.IsComplete);
        using var refused = await client.PostAsJsonAsync($"{Root}/{draft.Id}/publish", new PublishShippingRatePlanRequest(draft.Revision, "incomplete"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var preview = await PreviewAsync(client, draft);
        Assert.True(preview.IsPreview);
        Assert.False(preview.CanPublish);
        Assert.Equal(25000, preview.Calculation.TotalFee);
        Assert.Equal(ShippingRatePlanStatus.Draft, preview.Status);

        using var updatedResponse = await client.PutAsJsonAsync($"{Root}/{draft.Id}", Copy(definition, draft.Revision));
        var updated = await DataAsync<ShippingRatePlanDto>(updatedResponse);
        Assert.True(updated.Coverage.IsComplete);
        using var publishedResponse = await client.PostAsJsonAsync($"{Root}/{draft.Id}/publish", new PublishShippingRatePlanRequest(updated.Revision, "Publish synthetic rates"));
        var published = await DataAsync<ShippingRatePlanDto>(publishedResponse);
        Assert.Equal(ShippingRatePlanStatus.Published, published.Status);
        using var immutable = await client.PutAsJsonAsync($"{Root}/{draft.Id}", Copy(definition, published.Revision));
        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);
        var quoteId = Guid.NewGuid();
        await fixture.SeedAsync(context =>
        {
            var now = DateTimeOffset.UtcNow;
            context.Add(new ShippingQuote(quoteId, ShippingMethod.WorkspaceEC, new string('a', 64), new string('b', 64),
                10000, 1000, "{\"frozenFee\":10000}", published.Id, now, now.AddMinutes(10)));
            return Task.CompletedTask;
        });

        using var cloneResponse = await client.PostAsJsonAsync($"{Root}/{published.Id}/clone",
            new CloneShippingRatePlanRequest(published.Revision, "Replacement", "Clone synthetic rates"));
        var clone = await DataAsync<ShippingRatePlanDto>(cloneResponse);
        Assert.Equal(ShippingRatePlanStatus.Draft, clone.Status);
        Assert.NotEqual(published.Id, clone.Id);
        using var priceChange = await client.PutAsJsonAsync($"{Root}/{clone.Id}", Copy(definition, clone.Revision,
            [definition.Zones.Single() with { BaseFee = 20000 }]));
        var replacement = await DataAsync<ShippingRatePlanDto>(priceChange);
        using var staleActive = await client.PostAsJsonAsync($"{Root}/{clone.Id}/publish",
            new PublishShippingRatePlanRequest(replacement.Revision, "Missing active compare"));
        Assert.Equal(HttpStatusCode.Conflict, staleActive.StatusCode);
        using var replaceResponse = await client.PostAsJsonAsync($"{Root}/{clone.Id}/publish",
            new PublishShippingRatePlanRequest(replacement.Revision, "Replace synthetic rates", published.Id, published.Revision));
        var active = await DataAsync<ShippingRatePlanDto>(replaceResponse);
        Assert.Equal(ShippingRatePlanStatus.Published, active.Status);
        using var oldResponse = await client.GetAsync($"{Root}/{published.Id}");
        var old = await DataAsync<ShippingRatePlanDto>(oldResponse);
        Assert.Equal(ShippingRatePlanStatus.Retired, old.Status);
        Assert.Equal(10000, old.Zones.Single().BaseFee);
        var frozen = await fixture.ExecuteDbAsync(context => context.Set<ShippingQuote>().SingleAsync(quote => quote.Id == quoteId));
        Assert.Equal(10000, frozen.CustomerCharge);
        Assert.Equal(published.Id, frozen.RatePlanId);
        Assert.Equal(10000, JsonNode.Parse(frozen.SnapshotJson)!["frozenFee"]!.GetValue<int>());
        using var history = await client.GetAsync($"{Root}/{published.Id}/history");
        var audits = await DataAsync<WorkspaceEcommerce.Application.Common.Models.PagedResult<ShippingRateAuditDto>>(history);
        Assert.Equal(4, audits.TotalCount); // create, update, publish, replaced
        Assert.All(audits.Items, audit => Assert.StartsWith("admin-session:", audit.Actor));
        Assert.Contains(audits.Items, audit => audit.Action == "Replaced" && audit.ChangesJson.Contains("before"));

        using var retired = await client.PostAsJsonAsync($"{Root}/{active.Id}/retire", new ShippingRateActionRequest(active.Revision, "Stop new publication"));
        Assert.Equal(ShippingRatePlanStatus.Retired, (await DataAsync<ShippingRatePlanDto>(retired)).Status);
    }

    [Fact]
    public async Task ConcurrentPublishFromNoActivePlan_OneWinsAndOtherGetsConflict()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var definition = await DefinitionAsync(client);
        var a = await CreateAsync(client, definition);
        var b = await CreateAsync(client, definition);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"{Root}/{a.Id}/publish", new PublishShippingRatePlanRequest(a.Revision, "Publish A")),
            client.PostAsJsonAsync($"{Root}/{b.Id}/publish", new PublishShippingRatePlanRequest(b.Revision, "Publish B")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, await fixture.ExecuteDbAsync(context => context.Set<ShippingRatePlan>().CountAsync(plan => plan.Status == ShippingRatePlanStatus.Published)));
            Assert.Equal(1, await fixture.ExecuteDbAsync(context => context.Set<ShippingRateAudit>().CountAsync(audit => audit.Action == "Published")));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task ConcurrentDraftEdits_WithSameRevision_CannotLoseAnUpdate()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var definition = await DefinitionAsync(client);
        var draft = await CreateAsync(client, definition);
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"{Root}/{draft.Id}", Copy(definition, draft.Revision, name: "Edit A")),
            client.PutAsJsonAsync($"{Root}/{draft.Id}", Copy(definition, draft.Revision, name: "Edit B")));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(2, await fixture.ExecuteDbAsync(context => context.Set<ShippingRateAudit>().CountAsync()));
            Assert.Equal(definition.Zones.Single().ProvinceCodes.Count,
                await fixture.ExecuteDbAsync(context => context.Set<ShippingZoneProvince>().CountAsync()));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task EveryAdminOperation_RejectsAnonymousAndCustomerPrincipals()
    {
        await fixture.ResetDatabaseAsync();
        using var anonymous = fixture.CreateClient();
        using var customer = fixture.CreateClient();
        customer.UseBearerToken(await anonymous.RegisterCustomerAsync());
        var id = Guid.NewGuid();
        (HttpMethod Method, string Path)[] operations =
        [
            (HttpMethod.Get, "/api/admin/shipping/catalog"),
            (HttpMethod.Get, "/api/admin/shipping/catalog/provinces/01/wards"),
            (HttpMethod.Get, "/api/admin/shipping/catalog/weight-issues"),
            (HttpMethod.Get, Root), (HttpMethod.Get, $"{Root}/{id}"),
            (HttpMethod.Post, Root), (HttpMethod.Put, $"{Root}/{id}"),
            (HttpMethod.Post, $"{Root}/{id}/clone"), (HttpMethod.Get, $"{Root}/{id}/validation"),
            (HttpMethod.Post, $"{Root}/{id}/preview"), (HttpMethod.Post, $"{Root}/{id}/publish"),
            (HttpMethod.Post, $"{Root}/{id}/retire"), (HttpMethod.Get, $"{Root}/{id}/history")
        ];
        foreach (var operation in operations)
        {
            using var anonymousRequest = Request(operation.Method, operation.Path);
            using var customerRequest = Request(operation.Method, operation.Path);
            using var deniedAnonymous = await anonymous.SendAsync(anonymousRequest);
            using var deniedCustomer = await customer.SendAsync(customerRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, deniedAnonymous.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, deniedCustomer.StatusCode);
        }
        Assert.Equal(0, await fixture.ExecuteDbAsync(context => context.Set<ShippingRatePlan>().CountAsync()));
    }

    [Theory]
    [InlineData("null-zones")]
    [InlineData("null-zone")]
    [InlineData("null-provinces")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("catalog-version")]
    [InlineData("fractional-fee")]
    [InlineData("zero-step")]
    public async Task InvalidDefinitions_DoNotWritePlanOrAudit(string invalid)
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var body = JsonSerializer.SerializeToNode(await DefinitionAsync(client), JsonOptions)!;
        switch (invalid)
        {
            case "null-zones": body["zones"] = null; break;
            case "null-zone": body["zones"] = new JsonArray((JsonNode?)null); break;
            case "null-provinces": body["zones"]![0]!["provinceCodes"] = null; break;
            case "duplicate": body["zones"]![0]!["provinceCodes"] = new JsonArray("01", "01"); break;
            case "unknown": body["zones"]![0]!["provinceCodes"] = new JsonArray("99"); break;
            case "catalog-version": body["catalogVersion"] = "old"; break;
            case "fractional-fee": body["zones"]![0]!["baseFee"] = 0.5m; break;
            case "zero-step": body["zones"]![0]!["stepWeightGrams"] = 0; break;
        }
        using var response = await client.PostAsJsonAsync(Root, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await fixture.ExecuteDbAsync(context => context.Set<ShippingRatePlan>().CountAsync()));
        Assert.Equal(0, await fixture.ExecuteDbAsync(context => context.Set<ShippingRateAudit>().CountAsync()));
    }

    [Fact]
    public async Task CatalogPaginationAndPreview_ValidateParentAndRevisionWithoutIssuingQuote()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var draft = await CreateAsync(client, await DefinitionAsync(client));
        fixture.ResetSqlCommandCount();
        using var consistentRead = await client.GetAsync($"{Root}/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, consistentRead.StatusCode);
        // A split aggregate read can mix an old revision with newly committed zones.
        Assert.Equal(1, fixture.GetSqlSelectCount());
        using var wards = await client.GetAsync("/api/admin/shipping/catalog/provinces/01/wards?pageSize=1");
        var wardPage = await DataAsync<WorkspaceEcommerce.Application.Common.Models.PagedResult<ShippingWard>>(wards);
        Assert.Single(wardPage.Items);
        Assert.True(wardPage.TotalCount > 1);
        using var oversized = await client.GetAsync($"{Root}?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        using var unknownStatus = await client.GetAsync($"{Root}?status=999");
        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);
        using var badParent = await client.PostAsJsonAsync($"{Root}/{draft.Id}/preview", new ShippingRatePreviewRequest("79", "00004", draft.Revision, [new(1, 1)]));
        Assert.Equal(HttpStatusCode.BadRequest, badParent.StatusCode);
        using var stale = await client.PostAsJsonAsync($"{Root}/{draft.Id}/preview", new ShippingRatePreviewRequest("01", "00004", draft.Revision + 1, [new(1, 1)]));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(0, await fixture.ExecuteDbAsync(context => context.Set<ShippingQuote>().CountAsync()));
        using var openApi = await client.GetAsync("/openapi/v1.json");
        var document = await openApi.ReadJsonAsync();
        Assert.NotNull(document["paths"]?[$"{Root}/{{id}}/publish"]?["post"]?["responses"]?["409"]);
    }

    [Fact]
    public async Task ConfigurationPreview_DoesNotCallAnUnavailableCarrier()
    {
        await fixture.ResetDatabaseAsync();
        var unavailable = new UnavailableCarrier();
        using var baseFactory = fixture.CreateFactory(new Dictionary<string, string?>());
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IShipmentService>();
            services.AddSingleton<IShipmentService>(unavailable);
        }));
        using var client = factory.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        var draft = await CreateAsync(client, await DefinitionAsync(client));
        Assert.Equal(25000, (await PreviewAsync(client, draft)).Calculation.TotalFee);
        Assert.Equal(0, unavailable.Calls);
    }

    [Fact]
    public async Task FailedConfigurationTransaction_RollsBackItsIntermediateSave()
    {
        await fixture.ResetDatabaseAsync();
        using var client = await AdminAsync();
        var draft = await CreateAsync(client, await DefinitionAsync(client));
        using var publish = await client.PostAsJsonAsync($"{Root}/{draft.Id}/publish", new PublishShippingRatePlanRequest(draft.Revision, "Publish"));
        var published = await DataAsync<ShippingRatePlanDto>(publish);
        await fixture.ExecuteScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IShippingRateStore>();
            return await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteConfigurationTransactionAsync<int>(async token =>
            {
                var plan = await store.FindAsync(published.Id, token);
                plan!.Retire(DateTimeOffset.UtcNow);
                await store.SaveChangesAsync(token);
                throw new InvalidOperationException("Synthetic failure after the intermediate save.");
            }, CancellationToken.None));
        });
        Assert.Equal(ShippingRatePlanStatus.Published, await fixture.ExecuteDbAsync(context =>
            context.Set<ShippingRatePlan>().Where(plan => plan.Id == published.Id).Select(plan => plan.Status).SingleAsync()));
        Assert.Equal(2, await fixture.ExecuteDbAsync(context => context.Set<ShippingRateAudit>().CountAsync()));
    }

    [Fact]
    public async Task WeightReadinessReport_ListsMissingWeightsWithoutApplyingLegacyFallback()
    {
        await fixture.ResetDatabaseAsync();
        var category = new Category(Guid.NewGuid(), null, LocalizedText.Of("Synthetic"), "shipping-test", 1, true);
        var product = new Product(Guid.NewGuid(), category.Id, LocalizedText.Of("Synthetic"), "shipping-test-product", null, true);
        var missing = new ProductVariant(Guid.NewGuid(), product.Id, "MISSING-WEIGHT", "Missing", null, null,
            10000, null, 10, false, true);
        var complete = new ProductVariant(Guid.NewGuid(), product.Id, "KNOWN-WEIGHT", "Known", null, null,
            10000, null, 10, false, true, weightKg: 1m);
        await fixture.SeedAsync(context =>
        {
            context.AddRange(category, product, missing, complete);
            return Task.CompletedTask;
        });
        using var client = await AdminAsync();
        using var response = await client.GetAsync("/api/admin/shipping/catalog/weight-issues?pageSize=1");
        var page = await DataAsync<WorkspaceEcommerce.Application.Common.Models.PagedResult<ShippingWeightIssueDto>>(response);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(missing.Id, page.Items.Single().ProductVariantId);
        Assert.Null(page.Items.Single().WeightKg);
        using var invalid = await client.GetAsync("/api/admin/shipping/catalog/weight-issues?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private sealed class UnavailableCarrier : IShipmentService
    {
        public int Calls;
        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default) => Fail<ShippingQuoteResponse>();
        public Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default) => Fail<CreateShipmentResponse>();
        public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default) => Fail<TrackingResponse>();
        public Task<TrackingResponse> CancelShipmentAsync(string trackingCode, string reason, CancellationToken cancellationToken = default) => Fail<TrackingResponse>();
        private Task<T> Fail<T>()
        {
            Interlocked.Increment(ref Calls);
            return Task.FromException<T>(new HttpRequestException("Synthetic carrier outage."));
        }
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        return client;
    }
    private static async Task<SaveShippingRatePlanRequest> DefinitionAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/admin/shipping/catalog");
        var catalog = await DataAsync<ShippingCatalogSummaryDto>(response);
        return new SaveShippingRatePlanRequest
        {
            Name = "Synthetic nationwide rates", CatalogVersion = catalog.Version,
            OriginProvinceCode = "01", Reason = "Synthetic test configuration",
            Zones = [new("national", "National", 1000, 10000, 500, 5000, null, catalog.Provinces.Select(item => item.Code).ToArray())]
        };
    }
    private static SaveShippingRatePlanRequest Copy(SaveShippingRatePlanRequest request, long revision = 0,
        IReadOnlyCollection<ShippingRateZoneRequest>? zones = null, string? name = null) => new()
    {
        Name = name ?? request.Name, CatalogVersion = request.CatalogVersion, OriginProvinceCode = request.OriginProvinceCode,
        ExpectedRevision = revision, Reason = request.Reason, Zones = zones ?? request.Zones
    };
    private static async Task<ShippingRatePlanDto> CreateAsync(HttpClient client, SaveShippingRatePlanRequest definition)
    {
        using var response = await client.PostAsJsonAsync(Root, definition);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync<ShippingRatePlanDto>(response);
    }
    private static async Task<ShippingRatePreviewDto> PreviewAsync(HttpClient client, ShippingRatePlanDto plan)
    {
        using var response = await client.PostAsJsonAsync($"{Root}/{plan.Id}/preview", new ShippingRatePreviewRequest("01", "00004", plan.Revision, [new(2.001m, 1)]));
        return await DataAsync<ShippingRatePreviewDto>(response);
    }
    private static async Task<T> DataAsync<T>(HttpResponseMessage response)
    {
        var json = await response.ReadJsonAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {json}");
        return JsonSerializer.Deserialize<T>(json["data"]!.ToJsonString(), JsonOptions)!;
    }
    private static HttpRequestMessage Request(HttpMethod method, string path) => new(method, path)
    {
        Content = method == HttpMethod.Get ? null : JsonContent.Create(new { })
    };
}
