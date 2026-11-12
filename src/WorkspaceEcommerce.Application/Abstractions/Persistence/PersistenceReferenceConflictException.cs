namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public sealed class PersistenceReferenceConflictException(string message, Exception innerException)
    : Exception(message, innerException);
