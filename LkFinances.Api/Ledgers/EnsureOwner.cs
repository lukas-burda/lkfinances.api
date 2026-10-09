using LkFinances.Api.Auth;
using LkFinances.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Ledgers;

/// <summary>
/// The single authorization rule: only the ledger's owner reaches its routes.
/// Anyone else gets 404, so ledger ids do not leak.
/// </summary>
public sealed class EnsureOwner(FinanceDb db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!Guid.TryParse(http.GetRouteValue("ledgerId")?.ToString(), out var ledgerId))
            return Results.NotFound();

        var userId = http.User.Id();
        var owns = await db.Ledgers.AnyAsync(l => l.Id == ledgerId && l.OwnerUserId == userId, http.RequestAborted);
        return owns ? await next(context) : Results.NotFound();
    }
}
