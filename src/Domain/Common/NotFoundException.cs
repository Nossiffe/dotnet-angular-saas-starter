namespace Saas.Domain.Common;

public sealed class NotFoundException : Exception
{
    public string Entity { get; }
    public object Key { get; }

    public NotFoundException(string entity, object key)
        : base($"'{entity}' ({key}) introuvable.")
    {
        Entity = entity;
        Key = key;
    }

    public NotFoundException()
        : this("Entity", "unknown")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
        Entity = string.Empty;
        Key = string.Empty;
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
        Entity = string.Empty;
        Key = string.Empty;
    }
}
