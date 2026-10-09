using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Ledgers;

public sealed record CategoryDto(Guid Id, string Name, CategoryKind Kind);

public sealed record CategoryInput(string Name, CategoryKind Kind);

public static class CategoryEndpoints
{
    public static void MapCategories(this IEndpointRouteBuilder ledger)
    {
        ledger.MapGet("/categories", async (Guid ledgerId, FinanceDb db, CancellationToken ct) =>
            await db.Categories
                .Where(c => c.LedgerId == ledgerId)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Kind))
                .ToListAsync(ct));

        ledger.MapPost("/categories", async (Guid ledgerId, CategoryInput input, FinanceDb db, CancellationToken ct) =>
        {
            var category = new Category(ledgerId, input.Name ?? "", input.Kind);
            if (await db.Categories.AnyAsync(c => c.LedgerId == ledgerId && c.Name == category.Name, ct))
                throw new DomainException(nameof(CategoryInput.Name), "Já existe uma categoria com esse nome.");

            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/ledgers/{ledgerId}/categories/{category.Id}", new CategoryDto(category.Id, category.Name, category.Kind));
        });
    }

    public static async Task EnsureBelongsToLedgerAsync(this FinanceDb db, Guid ledgerId, Guid? categoryId, CancellationToken ct)
    {
        if (categoryId is { } id && !await db.Categories.AnyAsync(c => c.Id == id && c.LedgerId == ledgerId, ct))
            throw new DomainException("CategoryId", "Categoria não encontrada nesta carteira.");
    }
}
