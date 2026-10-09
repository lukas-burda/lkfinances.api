using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using LkFinances.Core.Import;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api;

/// <summary>
/// <c>import --file &lt;arquivo.csv|xlsx&gt; [--email &lt;dono&gt;]</c>.
/// Cabeçalho: data;descricao;valor;type;status;observacao
/// </summary>
public static class ImportCommand
{
    public const string Name = "import";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var file = Option(args, "--file");
        var email = Option(args, "--email") ?? services.GetRequiredService<IConfiguration>()["BOOTSTRAP_EMAIL"];
        if (file is null || email is null)
        {
            Console.Error.WriteLine("Uso: import --file <arquivo.csv|xlsx> [--email <dono>]");
            return 2;
        }

        var db = services.GetRequiredService<FinanceDb>();
        var normalized = User.NormalizeEmail(email);
        var ledgerId = await db.Ledgers
            .Where(l => db.Users.Any(u => u.Id == l.OwnerUserId && u.Email == normalized))
            .OrderBy(l => l.CreatedAt)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync();
        if (ledgerId is null)
        {
            Console.Error.WriteLine($"Nenhuma carteira encontrada para {normalized}.");
            return 1;
        }

        await using var stream = File.OpenRead(file);
        var result = await LedgerImporter.ImportAsync(db, ledgerId.Value, ImportReader.Read(stream, file), CancellationToken.None);
        Console.WriteLine(
            $"Lidas {result.Read}, inseridas {result.Inserted}, já importadas {result.AlreadyImported}, " +
            $"ignoradas {result.Skipped}, categorias novas {result.CategoriesCreated}.");
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
