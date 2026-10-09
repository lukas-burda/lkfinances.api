using ClosedXML.Excel;

namespace LkFinances.Api.Tests;

/// <summary>A short, anonymized workbook with the same shape as the real one.</summary>
public static class SpreadsheetFixture
{
    public static MemoryStream Build()
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.AddWorksheet("RESUMO");
        summary.Cell("K2").Value = "Real";
        summary.Cell("J3").Value = "RESERVA";
        summary.Cell("J4").Value = "INVESTIMENTOS";
        summary.Cell("J5").Value = "SOBRA";
        summary.Cell("K5").Value = 1500.0;

        var sheet = workbook.AddWorksheet("GASTOS");
        string[] header = ["01/06/2026", "DESCRIÇÃO", "VALOR", "Type", "Status", "Observação", "MÊS", "ANO"];
        for (var c = 0; c < header.Length; c++)
            sheet.Cell(1, c + 1).Value = header[c];

        var row = 2;
        void Add(DateTime date, string description, double value, string? type = null, string? status = null)
        {
            sheet.Cell(row, 1).Value = date;
            sheet.Cell(row, 2).Value = description;
            sheet.Cell(row, 3).Value = value;
            if (type is not null) sheet.Cell(row, 4).Value = type;
            if (status is not null) sheet.Cell(row, 5).Value = status;
            sheet.Cell(row, 7).Value = date.Month;
            sheet.Cell(row, 8).Value = date.Year;
            row++;
        }

        Add(new DateTime(2026, 1, 5), "Salário", 5000);
        Add(new DateTime(2026, 1, 10), "Mercado", -1081.8599999999999);
        Add(new DateTime(2026, 1, 10), "Café", -10);
        Add(new DateTime(2026, 1, 10), "Café", -10);
        Add(new DateTime(2026, 1, 15), "Ajuste Janeiro", -40, "AJUSTE");
        Add(new DateTime(2026, 1, 20), "Aporte reserva", 300, "RESERVA");
        Add(new DateTime(2026, 2, 1), "Compra ETF", 200, "INVESTIMENTO");
        Add(new DateTime(2026, 2, 2), "Item do museu", -999, "MUSEU");
        Add(new DateTime(2026, 2, 3), "Conta a vencer", -100, null, "PENDENTE");
        Add(new DateTime(2025, 12, 30), "Salário dezembro", 4000);
        Add(new DateTime(2026, 2, 4), "Linha zerada", 0);
        Add(new DateTime(2026, 2, 5), "", 50, "RESERVA");

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
