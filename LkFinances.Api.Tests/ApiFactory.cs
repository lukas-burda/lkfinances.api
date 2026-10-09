using System.Net.Http.Json;
using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LkFinances.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BootstrapEmail = "owner@test.local";
    public const string BootstrapPassword = "owner-password";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly string _keys = Path.Combine(Path.GetTempPath(), "lkf-keys-" + Guid.NewGuid());

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("DataProtection:KeysPath", _keys);
        builder.UseSetting("BOOTSTRAP_EMAIL", BootstrapEmail);
        builder.UseSetting("BOOTSTRAP_PASSWORD", BootstrapPassword);
    }

    public async Task<T> WithDbAsync<T>(Func<FinanceDb, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<FinanceDb>());
    }

    /// <summary>Creates an owner with one ledger and returns a client already signed in as them.</summary>
    public async Task<(HttpClient Client, Guid LedgerId)> SignedInOwnerAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        const string password = "secret";

        var ledgerId = await WithDbAsync(async db =>
        {
            var user = new User(email);
            user.SetPasswordHash(Services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password));
            var ledger = new Ledger(user.Id, Ledger.DefaultName);
            db.AddRange(user, ledger);
            await db.SaveChangesAsync();
            return ledger.Id;
        });

        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return (client, ledgerId);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
