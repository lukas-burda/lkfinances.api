using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LkFinances.Api.Auth;
using LkFinances.Api.Ledgers;
using LkFinances.Core.Domain;
using LkFinances.Core.Import;
using LkFinances.Core.Summary;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LkFinances.Api.Tests;

[Collection(ApiCollection.Name)]
public class ApiTests(ApiFactory api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Health_reports_database_reachable()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ledger_routes_require_login()
    {
        var response = await api.CreateClient().GetAsync("/api/ledgers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_owner_signs_in_and_has_the_principal_ledger()
    {
        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.BootstrapEmail, password = ApiFactory.BootstrapPassword });
        login.EnsureSuccessStatusCode();

        var ledgers = await client.GetFromJsonAsync<LedgerDto[]>("/api/ledgers", Json);

        Assert.Equal(Ledger.DefaultName, Assert.Single(ledgers!).Name);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.BootstrapEmail, password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_creates_principal_ledger_and_signs_in()
    {
        var client = api.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "new-password" });
        response.EnsureSuccessStatusCode();

        var ledgers = await client.GetFromJsonAsync<LedgerDto[]>("/api/ledgers", Json);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(email, me!.Email);
        Assert.Equal(Ledger.DefaultName, Assert.Single(ledgers!).Name);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = ApiFactory.BootstrapEmail, password = "another-password" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = $"short-{Guid.NewGuid():N}@test.local", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_does_not_reset_password_when_users_exist()
    {
        var hasher = api.Services.GetRequiredService<IPasswordHasher<User>>();
        var originalHash = await api.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == ApiFactory.BootstrapEmail);
            var hash = user.PasswordHash;
            user.SetPasswordHash(hasher.HashPassword(user, "changed-password"));
            await db.SaveChangesAsync();
            return hash;
        });

        try
        {
            await BootstrapUser.EnsureAsync(api.Services);

            var oldPassword = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { email = ApiFactory.BootstrapEmail, password = ApiFactory.BootstrapPassword });
            var newPassword = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { email = ApiFactory.BootstrapEmail, password = "changed-password" });

            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
            newPassword.EnsureSuccessStatusCode();
        }
        finally
        {
            await api.WithDbAsync(async db =>
            {
                var user = await db.Users.SingleAsync(u => u.Email == ApiFactory.BootstrapEmail);
                user.SetPasswordHash(originalHash);
                await db.SaveChangesAsync();
                return 0;
            });
        }
    }

    [Fact]
    public async Task Owner_can_change_password()
    {
        var email = $"pwd-{Guid.NewGuid():N}@test.local";
        var client = api.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/register", new { email, password = "old-password" })).EnsureSuccessStatusCode();

        var change = await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "old-password", newPassword = "new-password" });
        change.EnsureSuccessStatusCode();

        var stale = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "old-password" });
        var fresh = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "new-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        fresh.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Another_user_gets_404_on_someone_elses_ledger()
    {
        var (_, ownersLedger) = await api.SignedInOwnerAsync();
        var (intruder, _) = await api.SignedInOwnerAsync();

        var summary = await intruder.GetAsync($"/api/ledgers/{ownersLedger}/summary?year=2026");
        var create = await intruder.PostAsJsonAsync($"/api/ledgers/{ownersLedger}/transactions",
            new { occurredOn = "2026-01-01", description = "x", amount = 1, direction = "Out" });

        Assert.Equal(HttpStatusCode.NotFound, summary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Fact]
    public async Task Created_transaction_shows_in_the_grid_and_voiding_removes_it()
    {
        var (client, ledgerId) = await api.SignedInOwnerAsync();
        var created = await client.PostAsJsonAsync($"/api/ledgers/{ledgerId}/transactions",
            new { occurredOn = "2026-03-10", description = "Salário", amount = 1234.567m, direction = "In" });
        created.EnsureSuccessStatusCode();
        var transaction = (await created.Content.ReadFromJsonAsync<TransactionDto>(Json))!;

        var before = await client.GetFromJsonAsync<LedgerSummaryResult>($"/api/ledgers/{ledgerId}/summary?year=2026", Json);
        await client.PatchAsJsonAsync($"/api/ledgers/{ledgerId}/transactions/{transaction.Id}/status", new { status = "Voided" });
        var after = await client.GetFromJsonAsync<LedgerSummaryResult>($"/api/ledgers/{ledgerId}/summary?year=2026", Json);

        Assert.Equal(1234.57m, transaction.Amount);
        Assert.Equal(1234.57m, before!.Months[2].Income);
        Assert.Equal(0, after!.Months[2].Income);
    }

    [Fact]
    public async Task Invalid_transaction_returns_problem_details()
    {
        var (client, ledgerId) = await api.SignedInOwnerAsync();

        var response = await client.PostAsJsonAsync($"/api/ledgers/{ledgerId}/transactions",
            new { occurredOn = "2026-03-10", description = "Zero", amount = 0, direction = "Out" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Importing_the_same_spreadsheet_twice_adds_nothing_the_second_time()
    {
        var (client, ledgerId) = await api.SignedInOwnerAsync();

        var first = await ImportAsync(ledgerId);
        var second = await ImportAsync(ledgerId);
        var count = await api.WithDbAsync(db => db.Transactions.CountAsync(t => t.LedgerId == ledgerId));
        var summary = await client.GetFromJsonAsync<LedgerSummaryResult>($"/api/ledgers/{ledgerId}/summary?year=2026", Json);

        Assert.Equal(11, first.Inserted);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(11, second.AlreadyImported);
        Assert.Equal(11, count);
        Assert.Equal(new MonthLine(1, 5000, 1101.86m, 3898.14m), summary!.Months[0]);
        var surplus = summary.Balances.Single(b => b.Bucket == Bucket.Surplus);
        Assert.Equal(7858.14m, surplus.Computed);
        Assert.Equal(1500m, surplus.Asserted);
        Assert.Equal(350m, summary.Balances.Single(b => b.Bucket == Bucket.Reserve).Computed);
    }

    private Task<ImportResult> ImportAsync(Guid ledgerId) => api.WithDbAsync(db =>
    {
        using var stream = SpreadsheetFixture.Build();
        return LedgerImporter.ImportAsync(db, ledgerId, SpreadsheetReader.Read(stream), CancellationToken.None);
    });
}
