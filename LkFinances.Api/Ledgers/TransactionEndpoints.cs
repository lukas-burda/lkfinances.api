using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Ledgers;

public sealed record TransactionDto(
    Guid Id,
    DateOnly OccurredOn,
    string Description,
    string? Notes,
    decimal Amount,
    Direction Direction,
    TransactionStatus Status,
    Guid? CategoryId,
    string? CategoryName);

public sealed record TransactionInput(
    DateOnly OccurredOn,
    string Description,
    decimal Amount,
    Direction Direction,
    Guid? CategoryId,
    string? Notes,
    TransactionStatus Status = TransactionStatus.Active);

public sealed record StatusInput(TransactionStatus Status);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class TransactionEndpoints
{
    public const int PageSize = 50;

    public static void MapTransactions(this IEndpointRouteBuilder ledger)
    {
        ledger.MapGet("/transactions", async (
            Guid ledgerId, int? year, int? month, Guid? categoryId, TransactionStatus? status, int? page,
            FinanceDb db, CancellationToken ct) =>
        {
            if (month is < 1 or > 12)
                throw new DomainException(nameof(month), "Mês deve estar entre 1 e 12.");

            var query = db.Transactions.Where(t => t.LedgerId == ledgerId);
            if (year is { } y)
                query = query.Where(t => t.OccurredOn.Year == y);
            if (month is { } m)
                query = query.Where(t => t.OccurredOn.Month == m);
            if (categoryId is { } c)
                query = query.Where(t => t.CategoryId == c);
            if (status is { } s)
                query = query.Where(t => t.Status == s);

            var current = Math.Max(page ?? 1, 1);
            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(t => t.OccurredOn).ThenByDescending(t => t.CreatedAt)
                .Skip((current - 1) * PageSize).Take(PageSize)
                .Select(t => new TransactionDto(t.Id, t.OccurredOn, t.Description, t.Notes, t.Amount, t.Direction, t.Status, t.CategoryId, t.Category!.Name))
                .ToListAsync(ct);

            return new PagedList<TransactionDto>(items, current, PageSize, total);
        });

        ledger.MapPost("/transactions", async (Guid ledgerId, TransactionInput input, FinanceDb db, CancellationToken ct) =>
        {
            await db.EnsureBelongsToLedgerAsync(ledgerId, input.CategoryId, ct);
            var transaction = new Transaction(
                ledgerId, input.OccurredOn, input.Description ?? "", input.Amount, input.Direction,
                input.CategoryId, input.Notes, input.Status);

            db.Transactions.Add(transaction);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/ledgers/{ledgerId}/transactions/{transaction.Id}", await LoadAsync(db, transaction.Id, ct));
        });

        ledger.MapPut("/transactions/{id:guid}", async (Guid ledgerId, Guid id, TransactionInput input, FinanceDb db, CancellationToken ct) =>
        {
            var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == id && t.LedgerId == ledgerId, ct);
            if (transaction is null)
                return Results.NotFound();

            await db.EnsureBelongsToLedgerAsync(ledgerId, input.CategoryId, ct);
            transaction.Revise(input.OccurredOn, input.Description ?? "", input.Amount, input.Direction, input.CategoryId, input.Notes, input.Status);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, id, ct));
        });

        ledger.MapPatch("/transactions/{id:guid}/status", async (Guid ledgerId, Guid id, StatusInput input, FinanceDb db, CancellationToken ct) =>
        {
            var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == id && t.LedgerId == ledgerId, ct);
            if (transaction is null)
                return Results.NotFound();

            transaction.Revise(transaction.OccurredOn, transaction.Description, transaction.Amount, transaction.Direction, transaction.CategoryId, transaction.Notes, input.Status);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, id, ct));
        });
    }

    private static Task<TransactionDto> LoadAsync(FinanceDb db, Guid id, CancellationToken ct) =>
        db.Transactions
            .Where(t => t.Id == id)
            .Select(t => new TransactionDto(t.Id, t.OccurredOn, t.Description, t.Notes, t.Amount, t.Direction, t.Status, t.CategoryId, t.Category!.Name))
            .SingleAsync(ct);
}
