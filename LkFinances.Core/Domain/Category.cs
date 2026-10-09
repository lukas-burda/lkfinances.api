namespace LkFinances.Core.Domain;

/// <summary>Where a category's transactions count in the RESUMO.</summary>
public enum CategoryKind
{
    /// <summary>Monthly grid and Sobra.</summary>
    Regular,
    /// <summary>Sobra only (spreadsheet type AJUSTE).</summary>
    Adjustment,
    /// <summary>Reserva bucket only.</summary>
    Reserve,
    /// <summary>Investimentos bucket only.</summary>
    Investment,
    /// <summary>Recorded but counted nowhere (spreadsheet type MUSEU).</summary>
    Excluded,
}

public class Category
{
    public const int NameMaxLength = 100;

    private Category() { }

    public Category(Guid ledgerId, string name, CategoryKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(nameof(Name), "Informe o nome da categoria.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException(nameof(Name), $"O nome passa de {NameMaxLength} caracteres.");

        Id = Guid.CreateVersion7();
        LedgerId = ledgerId;
        Name = name.Trim();
        Kind = kind;
    }

    public Guid Id { get; private set; }
    public Guid LedgerId { get; private set; }
    public string Name { get; private set; } = "";
    public CategoryKind Kind { get; private set; }
}
