namespace LkFinances.Core.Domain;

public enum Bucket
{
    /// <summary>Sobra: cash not set aside.</summary>
    Surplus,
    /// <summary>Reserva.</summary>
    Reserve,
    /// <summary>Investimentos.</summary>
    Investment,
}

/// <summary>
/// The balance the owner actually sees for a bucket (spreadsheet column "Real").
/// It is compared against the computed balance, never used to change it.
/// </summary>
public class BalanceAssertion
{
    private BalanceAssertion() { }

    public BalanceAssertion(Guid ledgerId, Bucket bucket, decimal amount)
    {
        LedgerId = ledgerId;
        Bucket = bucket;
        Assert(amount);
    }

    public Guid LedgerId { get; private set; }
    public Bucket Bucket { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset AssertedAt { get; private set; }

    public void Assert(decimal amount)
    {
        Amount = Money.Round(amount);
        AssertedAt = DateTimeOffset.UtcNow;
    }
}
