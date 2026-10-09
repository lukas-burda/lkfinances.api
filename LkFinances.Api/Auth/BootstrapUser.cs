using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Auth;

/// <summary>
/// Seeds the first owner only when the database has no users.
/// After that, accounts come from register and passwords change via the API — not from env on restart.
/// </summary>
public static class BootstrapUser
{
    public static async Task EnsureAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(BootstrapUser));
        var db = services.GetRequiredService<FinanceDb>();

        if (await db.Users.AnyAsync(ct))
            return;

        var email = config["BOOTSTRAP_EMAIL"];
        var password = config["BOOTSTRAP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("No users yet and BOOTSTRAP_EMAIL/BOOTSTRAP_PASSWORD unset; the first account will come from register.");
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        Accounts.CreateWithPrincipalLedger(db, hasher, email, password);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created first user {Email} with the Principal ledger.", User.NormalizeEmail(email));
    }
}
