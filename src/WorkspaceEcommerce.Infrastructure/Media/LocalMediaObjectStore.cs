namespace WorkspaceEcommerce.Infrastructure.Media;

internal sealed class LocalMediaObjectStore(string rootPath) : IMediaObjectStore
{
    private readonly string _rootPath = Path.GetFullPath(rootPath);

    public async Task PutAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        var target = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, content.ToArray(), cancellationToken);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        var target = ResolvePath(objectKey);
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(ResolvePath(objectKey)));

    private string ResolvePath(string objectKey)
    {
        var target = Path.GetFullPath(Path.Combine(_rootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The media object key is outside the configured local media root.");
        }

        return target;
    }
}
