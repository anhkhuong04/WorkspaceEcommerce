using WorkspaceEcommerce.Application.Modules.Ordering.Receipts;

namespace WorkspaceEcommerce.Application.Abstractions.Documents;

public interface IOrderReceiptRenderer
{
    byte[] Render(OrderReceiptSnapshot receipt);
}

/// <summary>
/// Resolves receipt image snapshots to safe, decoded image bytes before the
/// PDF renderer is invoked. Implementations must never fetch arbitrary URLs.
/// </summary>
public interface IOrderReceiptImageResolver
{
    Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IReadOnlyCollection<string> imageUrls,
        CancellationToken cancellationToken = default);
}
