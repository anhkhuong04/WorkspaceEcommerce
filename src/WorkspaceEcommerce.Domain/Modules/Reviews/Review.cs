using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Reviews;

public sealed class Review : Entity
{
    public Review(
        Guid id,
        Guid productId,
        Guid customerId,
        int rating,
        string? comment)
        : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Review product id cannot be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("Review customer id cannot be empty.");
        }

        if (rating is < 1 or > 5)
        {
            throw new DomainException("Review rating must be between 1 and 5.");
        }

        ProductId = productId;
        CustomerId = customerId;
        Rating = rating;
        Comment = Guard.Optional(comment);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid ProductId { get; private set; }

    public Guid CustomerId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateContent(int rating, string? comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainException("Review rating must be between 1 and 5.");
        }

        Rating = rating;
        Comment = Guard.Optional(comment);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
