using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using LkFinances.Core.Domain;

namespace LkFinances.Core.Import;

public sealed record ImportRow(
    DateOnly OccurredOn,
    string Description,
    decimal SignedAmount,
    string? Type,
    TransactionStatus Status,
    string? Notes,
    string Hash);

public sealed record ImportFile(IReadOnlyList<ImportRow> Rows, int Skipped);

public static class ImportReader
{
    public static ImportFile Read(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(stream)
            : ReadXlsx(stream);
    }

    public static CategoryKind KindOf(string type) => Normalize(type) switch
    {
        "AJUSTE" => CategoryKind.Adjustment,
        "RESERVA" => CategoryKind.Reserve,
        "INVESTIMENTO" => CategoryKind.Investment,
        "MUSEU" => CategoryKind.Excluded,
        _ => CategoryKind.Regular,
    };

    private static ImportFile ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var lines = new List<string[]>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;
            lines.Add(Split(line, ';'));
        }

        if (lines.Count == 0)
            throw new InvalidDataException("O arquivo está vazio.");

        return FromRows(lines[0], lines.Skip(1).Select(cells => cells.Select(c => EmptyToNull(c.Trim())).ToArray()));
    }

    private static ImportFile ReadXlsx(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("A planilha não tem abas.");

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 1 || lastCol < 1)
            throw new InvalidDataException("A planilha está vazia.");

        var header = Enumerable.Range(1, lastCol).Select(c => sheet.Cell(1, c).GetString()).ToArray();
        var body = Enumerable.Range(2, Math.Max(0, lastRow - 1))
            .Select(r => Enumerable.Range(1, lastCol).Select(c => CellText(sheet.Cell(r, c))).ToArray());
        return FromRows(header, body);
    }

    private static ImportFile FromRows(IReadOnlyList<string> header, IEnumerable<string?[]> body)
    {
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < header.Count; i++)
        {
            var name = Normalize(header[i]);
            if (name.Length > 0 && !columns.ContainsKey(name))
                columns[name] = i;
        }

        int Col(string name) => columns.TryGetValue(name, out var i)
            ? i
            : throw new InvalidDataException($"Falta a coluna {name}.");
        int? Opt(string name) => columns.TryGetValue(name, out var i) ? i : null;

        var data = Col("DATA");
        var descricao = Col("DESCRICAO");
        var valor = Col("VALOR");
        var type = Opt("TYPE");
        var status = Opt("STATUS");
        var observacao = Opt("OBSERVACAO");

        var rows = new List<ImportRow>();
        var seen = new Dictionary<string, int>();
        var skipped = 0;

        foreach (var cells in body)
        {
            string? Cell(int? index) => index is { } i && i < cells.Length ? EmptyToNull(cells[i]) : null;

            var date = ParseDate(Cell(data));
            var description = Cell(descricao);
            var amount = ParseAmount(Cell(valor));

            if (date is null && description is null && amount is null)
                continue;
            if (date is null || description is null || amount is null or 0)
            {
                skipped++;
                continue;
            }

            var key = string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd}|{description}|{amount:F2}");
            var n = seen[key] = seen.GetValueOrDefault(key) + 1;
            rows.Add(new ImportRow(
                date.Value,
                description,
                amount.Value,
                Cell(type),
                ParseStatus(Cell(status)),
                Cell(observacao),
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{key}|{n}")))));
        }

        return new ImportFile(rows, skipped);
    }

    private static TransactionStatus ParseStatus(string? value) => Normalize(value ?? "") switch
    {
        "PENDENTE" => TransactionStatus.Pending,
        "ANULADO" => TransactionStatus.Voided,
        _ => TransactionStatus.Active,
    };

    private static DateOnly? ParseDate(string? text)
    {
        if (text is null)
            return null;
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (DateOnly.TryParseExact(text, formats, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out date))
            return date;
        return null;
    }

    private static decimal? ParseAmount(string? text)
    {
        if (text is null)
            return null;
        var value = text.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Trim();
        var culture = value.Contains(',') ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture;
        return decimal.TryParse(value, NumberStyles.Number, culture, out var amount)
            ? Money.Round(amount)
            : null;
    }

    private static string? CellText(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date))
            return DateOnly.FromDateTime(date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var number))
            return number.ToString("G", CultureInfo.InvariantCulture);
        return EmptyToNull(cell.GetString().Trim());
    }

    private static string[] Split(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                    quoted = !quoted;
            }
            else if (ch == delimiter && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(ch);
        }

        fields.Add(current.ToString());
        return [.. fields];
    }

    private static string? EmptyToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

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
}
