using LkFinances.Core.Domain;
using LkFinances.Core.Summary;

namespace LkFinances.Api.Tests;

public class LedgerSummaryTests
{
    private static SummaryRow Row(int year, int month, CategoryKind kind, Direction direction, decimal total, TransactionStatus status = TransactionStatus.Active) =>
        new(year, month, kind, status, direction, total);

    private static readonly SummaryRow[] Rows =
    [
        Row(2026, 1, CategoryKind.Regular, Direction.In, 5000),
        Row(2026, 1, CategoryKind.Regular, Direction.Out, 1101.86m),
        Row(2026, 1, CategoryKind.Adjustment, Direction.Out, 40),
        Row(2026, 1, CategoryKind.Reserve, Direction.In, 300),
        Row(2026, 2, CategoryKind.Investment, Direction.In, 200),
        Row(2026, 2, CategoryKind.Excluded, Direction.Out, 999),
        Row(2026, 2, CategoryKind.Regular, Direction.Out, 100, TransactionStatus.Pending),
        Row(2025, 12, CategoryKind.Regular, Direction.In, 4000),
    ];

    private static readonly Dictionary<Bucket, decimal> Assertions = new() { [Bucket.Surplus] = 7900 };

    [Fact]
    public void Monthly_grid_counts_only_regular_categories_of_the_year()
    {
        var summary = LedgerSummary.Compose(2026, Rows, Assertions);

        Assert.Equal(new MonthLine(1, 5000, 1101.86m, 3898.14m), summary.Months[0]);
        Assert.Equal(new MonthLine(2, 0, 100, -100), summary.Months[1]);
        Assert.Equal(12, summary.Months.Count);
        Assert.Equal(3798.14m, summary.YearNet);
    }

    [Fact]
    public void Balances_span_all_years_and_follow_the_spreadsheet_buckets()
    {
        var balances = LedgerSummary.Compose(2026, Rows, Assertions).Balances.ToDictionary(b => b.Bucket);

        Assert.Equal(5000 - 1101.86m - 40 + 4000, balances[Bucket.Surplus].Computed);
        Assert.Equal(300, balances[Bucket.Reserve].Computed);
        Assert.Equal(200, balances[Bucket.Investment].Computed);
    }

    [Fact]
    public void Difference_is_real_minus_computed_and_absent_without_assertion()
    {
        var balances = LedgerSummary.Compose(2026, Rows, Assertions).Balances.ToDictionary(b => b.Bucket);

        Assert.Equal(7900 - 7858.14m, balances[Bucket.Surplus].Difference);
        Assert.Null(balances[Bucket.Reserve].Asserted);
        Assert.Null(balances[Bucket.Reserve].Difference);
    }

    [Fact]
    public void Voided_rows_count_nowhere()
    {
        Assert.False(SummaryRules.CountsInMonthlyGrid(CategoryKind.Regular, TransactionStatus.Voided));
        Assert.Null(SummaryRules.BucketOf(CategoryKind.Reserve, TransactionStatus.Voided));
    }
}
