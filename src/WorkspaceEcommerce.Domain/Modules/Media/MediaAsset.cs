using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Media;

public sealed class MediaAsset : Entity
{
    private readonly List<MediaAssetVariant> _variants = [];

    private MediaAsset()
    {
    }

    public MediaAsset(
        Guid id,
        string folder,
        string objectKey,
        string publicUrl,
        string contentType,
        string checksum,
        long size,
        int width,
        int height,
        int frameCount,
        string? createdBy)
        : base(id)
    {
        Folder = Guard.Required(folder, nameof(Folder));
        ObjectKey = Guard.Required(objectKey, nameof(ObjectKey));
        PublicUrl = Guard.Required(publicUrl, nameof(PublicUrl));
        ContentType = Guard.Required(contentType, nameof(ContentType));
        Checksum = Guard.Required(checksum, nameof(Checksum));
        Size = size;
        Width = width;
        Height = height;
        FrameCount = frameCount;
        CreatedBy = Guard.Optional(createdBy);
        State = MediaAssetState.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Folder { get; private set; } = default!;

    public string ObjectKey { get; private set; } = default!;

    public string PublicUrl { get; private set; } = default!;

    public string ContentType { get; private set; } = default!;

    public string Checksum { get; private set; } = default!;

    public long Size { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int FrameCount { get; private set; }

    public string? CreatedBy { get; private set; }

    public MediaAssetState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AvailableAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyCollection<MediaAssetVariant> Variants => _variants;

    public void AddVariant(string name, string objectKey, string publicUrl, int width, int height, long size)
    {
        _variants.Add(new MediaAssetVariant(Guid.NewGuid(), Id, name, objectKey, publicUrl, width, height, size));
    }

    public void MarkAvailable()
    {
        State = MediaAssetState.Available;
        AvailableAt = DateTimeOffset.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        State = MediaAssetState.Failed;
        FailureReason = Guard.Required(reason, nameof(reason));
    }

    public void MarkDeleted()
    {
        State = MediaAssetState.Deleted;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class MediaAssetVariant : Entity
{
    private MediaAssetVariant()
    {
    }

    internal MediaAssetVariant(Guid id, Guid mediaAssetId, string name, string objectKey, string publicUrl, int width, int height, long size)
        : base(id)
    {
        MediaAssetId = mediaAssetId;
        Name = Guard.Required(name, nameof(Name));
        ObjectKey = Guard.Required(objectKey, nameof(ObjectKey));
        PublicUrl = Guard.Required(publicUrl, nameof(PublicUrl));
        Width = width;
        Height = height;
        Size = size;
    }

    public Guid MediaAssetId { get; private set; }
    public string Name { get; private set; } = default!;
    public string ObjectKey { get; private set; } = default!;
    public string PublicUrl { get; private set; } = default!;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long Size { get; private set; }
}

public enum MediaAssetState
{
    Pending = 0,
    Available = 1,
    Failed = 2,
    Deleted = 3
}
