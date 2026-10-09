namespace LkFinances.Core.Domain;

public class Ledger
{
    public const string DefaultName = "Principal";

    private Ledger() { }

    public Ledger(Guid ownerUserId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(nameof(Name), "Informe o nome da carteira.");

        Id = Guid.CreateVersion7();
        OwnerUserId = ownerUserId;
        Name = name.Trim();
        Currency = Money.Currency;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = "";
    public string Currency { get; private set; } = Money.Currency;
    public DateTimeOffset CreatedAt { get; private set; }
}
