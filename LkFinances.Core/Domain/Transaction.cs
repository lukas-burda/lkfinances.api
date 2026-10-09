namespace LkFinances.Core.Domain;

public enum Direction
{
    In,
    Out,
}

public enum TransactionStatus
{
    Active,
    /// <summary>Not yet settled; left out of Sobra (spreadsheet status PENDENTE).</summary>
    Pending,
    /// <summary>Cancelled; counted nowhere. Replaces deletion.</summary>
    Voided,
}

public class Transaction
{
    public const int DescriptionMaxLength = 500;
    public const int NotesMaxLength = 2000;

    private Transaction() { }

    public Transaction(
        Guid ledgerId,
        DateOnly occurredOn,
        string description,
        decimal amount,
        Direction direction,
        Guid? categoryId = null,
        string? notes = null,
        TransactionStatus status = TransactionStatus.Active,
        string? importHash = null)
    {
        Id = Guid.CreateVersion7();
        LedgerId = ledgerId;
        ImportHash = importHash;
        CreatedAt = DateTimeOffset.UtcNow;
        Revise(occurredOn, description, amount, direction, categoryId, notes, status);
    }

    public Guid Id { get; private set; }
    public Guid LedgerId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string Description { get; private set; } = "";
    public string? Notes { get; private set; }
    public decimal Amount { get; private set; }
    public Direction Direction { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string? ImportHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Revise(
        DateOnly occurredOn,
        string description,
        decimal amount,
        Direction direction,
        Guid? categoryId,
        string? notes,
        TransactionStatus status)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException(nameof(Description), "Informe a descrição.");
        if (description.Trim().Length > DescriptionMaxLength)
            throw new DomainException(nameof(Description), $"A descrição passa de {DescriptionMaxLength} caracteres.");
        if (notes?.Length > NotesMaxLength)
            throw new DomainException(nameof(Notes), $"A observação passa de {NotesMaxLength} caracteres.");

        var rounded = Money.Round(amount);
        if (rounded <= 0)
            throw new DomainException(nameof(Amount), "O valor deve ser maior que zero; a direção diz se é entrada ou saída.");

        OccurredOn = occurredOn;
        Description = description.Trim();
        Amount = rounded;
        Direction = direction;
        CategoryId = categoryId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = status;
    }
}
