using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Core.Import;

public sealed record ImportResult(int Read, int Inserted, int AlreadyImported, int Skipped, int CategoriesCreated, int BalancesAsserted);

public static class LedgerImporter
{
    public static async Task<ImportResult> ImportAsync(FinanceDb db, Guid ledgerId, SpreadsheetData data, CancellationToken ct)
    {
        var categories = await db.Categories
            .Where(c => c.LedgerId == ledgerId)
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, ct);
        var categoriesBefore = categories.Count;

        var hashes = data.Rows.Select(r => r.Hash).ToList();
        var existing = (await db.Transactions
                .Where(t => t.LedgerId == ledgerId && t.ImportHash != null && hashes.Contains(t.ImportHash))
                .Select(t => t.ImportHash!)
                .ToListAsync(ct))
            .ToHashSet();

        var inserted = 0;
        foreach (var row in data.Rows.Where(r => !existing.Contains(r.Hash)))
        {
            Guid? categoryId = null;
            if (row.Type is { } type)
            {
                if (!categories.TryGetValue(type, out var category))
                {
                    category = new Category(ledgerId, type, SpreadsheetReader.KindOf(type));
                    categories[type] = category;
                    db.Categories.Add(category);
                }
                categoryId = category.Id;
            }

            db.Transactions.Add(new Transaction(
                ledgerId,
                row.OccurredOn,
                row.Description,
                Math.Abs(row.SignedAmount),
                row.SignedAmount > 0 ? Direction.In : Direction.Out,
                categoryId,
                row.Notes,
                row.Status,
                row.Hash));
            inserted++;
        }

        var assertions = await db.BalanceAssertions.Where(b => b.LedgerId == ledgerId).ToDictionaryAsync(b => b.Bucket, ct);
        foreach (var (bucket, amount) in data.RealBalances)
        {
            if (assertions.TryGetValue(bucket, out var assertion))
                assertion.Assert(amount);
            else
                db.BalanceAssertions.Add(new BalanceAssertion(ledgerId, bucket, amount));
        }

        await db.SaveChangesAsync(ct);
        return new ImportResult(data.Rows.Count, inserted, existing.Count, data.SkippedRows, categories.Count - categoriesBefore, data.RealBalances.Count);
    }
}
