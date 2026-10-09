using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using LkFinances.Core.Summary;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Ledgers;

/// <summary>A null amount removes the assertion for that bucket.</summary>
public sealed record BalanceInput(Bucket Bucket, decimal? Amount);

public static class SummaryEndpoints
{
    public static void MapSummary(this IEndpointRouteBuilder ledger)
    {
        ledger.MapGet("/summary", (Guid ledgerId, int? year, FinanceDb db, CancellationToken ct) =>
            LedgerSummary.LoadAsync(db, ledgerId, year ?? DateTime.Today.Year, ct));

        ledger.MapPut("/balances", async (Guid ledgerId, BalanceInput[] input, FinanceDb db, CancellationToken ct) =>
        {
            var assertions = await db.BalanceAssertions.Where(b => b.LedgerId == ledgerId).ToDictionaryAsync(b => b.Bucket, ct);
            foreach (var (bucket, amount) in input)
            {
                assertions.TryGetValue(bucket, out var assertion);
                switch (amount, assertion)
                {
                    case (null, not null):
                        db.BalanceAssertions.Remove(assertion);
                        break;
                    case ({ } value, not null):
                        assertion.Assert(value);
                        break;
                    case ({ } value, null):
                        db.BalanceAssertions.Add(new BalanceAssertion(ledgerId, bucket, value));
                        break;
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
