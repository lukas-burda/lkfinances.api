using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Core.Import;

public sealed record ImportResult(int Read, int Inserted, int AlreadyImported, int Skipped, int CategoriesCreated);

public static class LedgerImporter
{
    public static async Task<ImportResult> ImportAsync(FinanceDb db, Guid ledgerId, ImportFile file, CancellationToken ct)
    {
        var categories = await db.Categories
            .Where(c => c.LedgerId == ledgerId)
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, ct);
        var categoriesBefore = categories.Count;

        var hashes = file.Rows.Select(r => r.Hash).ToList();
        var existing = (await db.Transactions
                .Where(t => t.LedgerId == ledgerId && t.ImportHash != null && hashes.Contains(t.ImportHash))
                .Select(t => t.ImportHash!)
                .ToListAsync(ct))
            .ToHashSet();

        var inserted = 0;
        foreach (var row in file.Rows.Where(r => !existing.Contains(r.Hash)))
        {
            Guid? categoryId = null;
            if (row.Type is { } type)
            {
                if (!categories.TryGetValue(type, out var category))
                {
                    category = new Category(ledgerId, type, ImportReader.KindOf(type));
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

        await db.SaveChangesAsync(ct);
        return new ImportResult(file.Rows.Count, inserted, existing.Count, file.Skipped, categories.Count - categoriesBefore);
    }
}
