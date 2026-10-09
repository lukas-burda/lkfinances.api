using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Core.Summary;

public sealed record SummaryRow(int Year, int Month, CategoryKind Kind, TransactionStatus Status, Direction Direction, decimal Total);

public sealed record MonthLine(int Month, decimal Income, decimal Expenses, decimal Net);

public sealed record BucketLine(Bucket Bucket, decimal Computed, decimal? Asserted, decimal? Difference);

public sealed record LedgerSummaryResult(
    int Year,
    IReadOnlyList<MonthLine> Months,
    decimal YearIncome,
    decimal YearExpenses,
    decimal YearNet,
    IReadOnlyList<BucketLine> Balances,
    decimal TotalBalance);

public static class LedgerSummary
{
    public static async Task<LedgerSummaryResult> LoadAsync(FinanceDb db, Guid ledgerId, int year, CancellationToken ct)
    {
        var grouped = await db.Transactions
            .Where(t => t.LedgerId == ledgerId && t.Status != TransactionStatus.Voided)
            .GroupBy(t => new
            {
                t.OccurredOn.Year,
                t.OccurredOn.Month,
                Kind = (CategoryKind?)t.Category!.Kind,
                t.Status,
                t.Direction,
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Kind, g.Key.Status, g.Key.Direction, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var assertions = await db.BalanceAssertions
            .Where(b => b.LedgerId == ledgerId)
            .ToDictionaryAsync(b => b.Bucket, b => b.Amount, ct);

        var rows = grouped.Select(g => new SummaryRow(g.Year, g.Month, g.Kind ?? CategoryKind.Regular, g.Status, g.Direction, g.Total));
        return Compose(year, rows, assertions);
    }

    public static LedgerSummaryResult Compose(int year, IEnumerable<SummaryRow> rows, IReadOnlyDictionary<Bucket, decimal> assertions)
    {
        var all = rows.ToList();

        var grid = all.Where(r => r.Year == year && SummaryRules.CountsInMonthlyGrid(r.Kind, r.Status)).ToList();
        var months = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var income = grid.Where(r => r.Month == month && r.Direction == Direction.In).Sum(r => r.Total);
                var expenses = grid.Where(r => r.Month == month && r.Direction == Direction.Out).Sum(r => r.Total);
                return new MonthLine(month, income, expenses, income - expenses);
            })
            .ToList();

        var balances = Enum.GetValues<Bucket>()
            .Select(bucket =>
            {
                var computed = all
                    .Where(r => SummaryRules.BucketOf(r.Kind, r.Status) == bucket)
                    .Sum(r => r.Direction == Direction.In ? r.Total : -r.Total);
                decimal? asserted = assertions.TryGetValue(bucket, out var value) ? value : null;
                return new BucketLine(bucket, computed, asserted, asserted - computed);
            })
            .ToList();

        var yearIncome = months.Sum(m => m.Income);
        var yearExpenses = months.Sum(m => m.Expenses);
        return new LedgerSummaryResult(year, months, yearIncome, yearExpenses, yearIncome - yearExpenses, balances, balances.Sum(b => b.Computed));
    }
}
