namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public sealed class PersistenceConcurrencyException : Exception
{
    public PersistenceConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
