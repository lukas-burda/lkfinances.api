using System.Security.Claims;
using LkFinances.Api.Auth;
using LkFinances.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Ledgers;

public sealed record LedgerDto(Guid Id, string Name, string Currency);

public static class LedgerEndpoints
{
    public static void MapLedgers(this IEndpointRouteBuilder app)
    {
        var ledgers = app.MapGroup("/api/ledgers").RequireAuthorization();

        ledgers.MapGet("/", async (ClaimsPrincipal user, FinanceDb db, CancellationToken ct) =>
        {
            var userId = user.Id();
            return await db.Ledgers
                .Where(l => l.OwnerUserId == userId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new LedgerDto(l.Id, l.Name, l.Currency))
                .ToListAsync(ct);
        });

        var ledger = ledgers.MapGroup("/{ledgerId:guid}").AddEndpointFilter<EnsureOwner>();
        ledger.MapTransactions();
        ledger.MapCategories();
        ledger.MapSummary();
    }
}
