using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using LkFinances.Core.Domain;

namespace LkFinances.Core.Import;

public sealed record SpreadsheetRow(
    int SheetRow,
    DateOnly OccurredOn,
    string Description,
    decimal SignedAmount,
    string? Type,
    TransactionStatus Status,
    string? Notes,
    string Hash);

public sealed record SpreadsheetData(IReadOnlyList<SpreadsheetRow> Rows, IReadOnlyDictionary<Bucket, decimal> RealBalances, int SkippedRows);

/// <summary>Reads the GASTOS sheet (one row per transaction) and the "Real" column of the RESUMO sheet.</summary>
public static class SpreadsheetReader
{
    public const string TransactionsSheet = "GASTOS";
    public const string SummarySheet = "RESUMO";
    public const string MissingDescription = "(sem descrição)";

    public static SpreadsheetData Read(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sheet = FindSheet(workbook, TransactionsSheet)
            ?? throw new InvalidDataException($"A planilha não tem a aba {TransactionsSheet}.");

        var (rows, skipped) = ReadTransactions(sheet);
        var summary = FindSheet(workbook, SummarySheet);
        var real = summary is null ? new Dictionary<Bucket, decimal>() : ReadRealBalances(summary);
        return new SpreadsheetData(rows, real, skipped);
    }

    public static CategoryKind KindOf(string type) => Normalize(type) switch
    {
        "AJUSTE" => CategoryKind.Adjustment,
        "RESERVA" => CategoryKind.Reserve,
        "INVESTIMENTO" => CategoryKind.Investment,
        "MUSEU" => CategoryKind.Excluded,
        _ => CategoryKind.Regular,
    };

    private static (List<SpreadsheetRow> Rows, int Skipped) ReadTransactions(IXLWorksheet sheet)
    {
        var header = sheet.Row(1);
        var lastColumn = header.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var columns = Enumerable.Range(1, lastColumn)
            .Select(c => (Name: Normalize(header.Cell(c).GetString()), Column: c))
            .Where(h => h.Name.Length > 0)
            .GroupBy(h => h.Name)
            .ToDictionary(g => g.Key, g => g.First().Column);

        int Required(string name) => columns.TryGetValue(name, out var c)
            ? c
            : throw new InvalidDataException($"A aba {sheet.Name} não tem a coluna {name}.");
        int? Optional(string name) => columns.TryGetValue(name, out var c) ? c : null;

        const int dateColumn = 1;
        var descriptionColumn = Required("DESCRICAO");
        var amountColumn = Required("VALOR");
        var typeColumn = Optional("TYPE");
        var statusColumn = Optional("STATUS");
        var notesColumn = Optional("OBSERVACAO");

        var rows = new List<SpreadsheetRow>();
        var occurrences = new Dictionary<string, int>();
        var skipped = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var description = row.Cell(descriptionColumn).GetString().Trim();
            var date = ReadDate(row.Cell(dateColumn));
            var amount = ReadAmount(row.Cell(amountColumn));

            if (description.Length == 0 && date is null && amount is null)
                continue;
            if (date is null || amount is null or 0)
            {
                skipped++;
                continue;
            }
            if (description.Length == 0)
                description = MissingDescription;

            var key = string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd}|{description}|{amount:F2}");
            var occurrence = occurrences[key] = occurrences.GetValueOrDefault(key) + 1;

            rows.Add(new SpreadsheetRow(
                r,
                date.Value,
                description,
                amount.Value,
                Text(row, typeColumn),
                Normalize(Text(row, statusColumn) ?? "") == "PENDENTE" ? TransactionStatus.Pending : TransactionStatus.Active,
                Text(row, notesColumn),
                Sha256($"{key}|{occurrence}")));
        }

        return (rows, skipped);
    }

    private static Dictionary<Bucket, decimal> ReadRealBalances(IXLWorksheet sheet)
    {
        var balances = new Dictionary<Bucket, decimal>();
        var realHeader = sheet.CellsUsed().FirstOrDefault(c => Normalize(c.GetString()) == "REAL");
        if (realHeader is null || realHeader.Address.ColumnNumber < 2)
            return balances;

        var column = realHeader.Address.ColumnNumber;
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = realHeader.Address.RowNumber + 1; r <= last; r++)
        {
            Bucket? bucket = Normalize(sheet.Cell(r, column - 1).GetString()) switch
            {
                "SOBRA" => Bucket.Surplus,
                "RESERVA" => Bucket.Reserve,
                "INVESTIMENTOS" => Bucket.Investment,
                _ => null,
            };
            if (bucket is not null && ReadAmount(sheet.Cell(r, column)) is { } amount)
                balances[bucket.Value] = amount;
        }

        return balances;
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(s => Normalize(s.Name) == name);

    private static DateOnly? ReadDate(IXLCell cell)
    {
        if (cell.TryGetValue<DateTime>(out var dateTime))
            return DateOnly.FromDateTime(dateTime);
        if (cell.TryGetValue<double>(out var serial) && serial > 0)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        return null;
    }

    private static decimal? ReadAmount(IXLCell cell) =>
        cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var value)
            ? Money.Round((decimal)value)
            : null;

    private static string? Text(IXLRow row, int? column)
    {
        if (column is null)
            return null;
        var text = row.Cell(column.Value).GetString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static string Normalize(string text)
    {
        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(ch));
        }
        return builder.ToString();
    }

    private static string Sha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
