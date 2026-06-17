namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public interface ICurrentCustomerContext
{
    Guid? CustomerId { get; }

    string? Email { get; }
}
