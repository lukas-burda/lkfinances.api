namespace LkFinances.Core.Domain;

public sealed class DomainException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
