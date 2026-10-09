using LkFinances.Core.Domain;

namespace LkFinances.Core.Summary;

/// <summary>The RESUMO formulas of the spreadsheet, as rules over a transaction's category kind and status.</summary>
public static class SummaryRules
{
    public static bool CountsInMonthlyGrid(CategoryKind kind, TransactionStatus status) =>
        status != TransactionStatus.Voided && kind == CategoryKind.Regular;

    public static Bucket? BucketOf(CategoryKind kind, TransactionStatus status) => (kind, status) switch
    {
        (_, TransactionStatus.Voided) => null,
        (CategoryKind.Regular or CategoryKind.Adjustment, TransactionStatus.Pending) => null,
        (CategoryKind.Regular or CategoryKind.Adjustment, _) => Bucket.Surplus,
        (CategoryKind.Reserve, _) => Bucket.Reserve,
        (CategoryKind.Investment, _) => Bucket.Investment,
        _ => null,
    };
}
