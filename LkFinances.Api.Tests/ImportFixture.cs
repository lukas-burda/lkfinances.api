using System.Text;
using ClosedXML.Excel;

namespace LkFinances.Api.Tests;

public static class ImportFixture
{
    public const string Csv = """
        data;descricao;valor;type;status;observacao
        2026-01-05;Salário;5000;;;
        10/01/2026;Mercado;-1081,8599999999999;;;
        10/01/2026;Café;-10;;;
        10/01/2026;Café;-10;;;
        15/01/2026;Ajuste Janeiro;-40;AJUSTE;;
        20/01/2026;Aporte reserva;300;RESERVA;;
        2026-02-01;Compra ETF;200;INVESTIMENTO;;
        02/02/2026;Item do museu;-999;MUSEU;;
        03/02/2026;Conta a vencer;-100;;PENDENTE;
        30/12/2025;Salário dezembro;4000;;;nota
        04/02/2026;Linha zerada;0;;;
        05/02/2026;;50;RESERVA;;
        """;

    public static MemoryStream CsvStream() => new(Encoding.UTF8.GetBytes(Csv));

    public static MemoryStream XlsxStream()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");
        var lines = Csv.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var r = 0; r < lines.Length; r++)
        {
            var cells = lines[r].Split(';');
            for (var c = 0; c < cells.Length; c++)
                sheet.Cell(r + 1, c + 1).Value = cells[c];
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
