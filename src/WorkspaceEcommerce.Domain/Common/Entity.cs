namespace WorkspaceEcommerce.Domain.Common;

public abstract class Entity
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Entity id cannot be empty.");
        }

        Id = id;
    }

    public Guid Id { get; protected set; }
}
