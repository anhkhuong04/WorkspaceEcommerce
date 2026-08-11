using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Warranties;

public sealed class WarrantyImportBatch : Entity
{
    public WarrantyImportBatch(
        Guid id,
        string contentChecksum,
        string requestedBy,
        int totalRows,
        DateTimeOffset createdAt)
        : base(id)
    {
        ContentChecksum = Guard.Required(contentChecksum, nameof(ContentChecksum));
        RequestedBy = Guard.Required(requestedBy, nameof(RequestedBy));
        if (totalRows is < 1 or > 10_000)
        {
            throw new DomainException("Warranty import batch must contain between 1 and 10,000 rows.");
        }

        if (createdAt == default)
        {
            throw new DomainException("Warranty import batch timestamp is required.");
        }

        TotalRows = totalRows;
        CreatedAt = createdAt;
        ImportedRows = 0;
        FailedRows = 0;
    }

    public string ContentChecksum { get; private set; }

    public string RequestedBy { get; private set; }

    public int TotalRows { get; private set; }

    public int ImportedRows { get; private set; }

    public int FailedRows { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(int importedRows, int failedRows, DateTimeOffset completedAt)
    {
        if (importedRows < 0 || failedRows < 0 || importedRows + failedRows != TotalRows || completedAt == default)
        {
            throw new DomainException("Warranty import batch completion values are invalid.");
        }

        ImportedRows = importedRows;
        FailedRows = failedRows;
        CompletedAt = completedAt;
    }
}
