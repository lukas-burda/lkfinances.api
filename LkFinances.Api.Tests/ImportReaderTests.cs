using System.Text;
using LkFinances.Core.Domain;
using LkFinances.Core.Import;

namespace LkFinances.Api.Tests;

public class ImportReaderTests
{
    private static ImportFile ReadCsv()
    {
        using var stream = ImportFixture.CsvStream();
        return ImportReader.Read(stream, "gastos.csv");
    }

    [Fact]
    public void Parses_brazilian_currency_with_rs_prefix()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            data;descricao;valor;type;status;observacao
            07/07/2022;Salário; R$ 4.090,79 ;;;
            09/08/2022;Consórcio;-R$ 585,30 ;CONSÓRCIO;;
            """));

        var file = ImportReader.Read(stream, "gastos.csv");

        Assert.Equal(4090.79m, file.Rows.Single(r => r.Description == "Salário").SignedAmount);
        Assert.Equal(-585.30m, file.Rows.Single(r => r.Description == "Consórcio").SignedAmount);
    }

    [Fact]
    public void Reads_csv_rounding_amounts_and_skipping_empty_or_zero_lines()
    {
        var file = ReadCsv();

        Assert.Equal(10, file.Rows.Count);
        Assert.Equal(2, file.Skipped);
        Assert.Equal(-1081.86m, file.Rows.Single(r => r.Description == "Mercado").SignedAmount);
        Assert.Equal(new DateOnly(2025, 12, 30), file.Rows.Single(r => r.Description == "Salário dezembro").OccurredOn);
        Assert.Equal("nota", file.Rows.Single(r => r.Description == "Salário dezembro").Notes);
    }

    [Fact]
    public void Identical_rows_get_distinct_hashes()
    {
        var coffees = ReadCsv().Rows.Where(r => r.Description == "Café").ToList();

        Assert.Equal(2, coffees.Count);
        Assert.NotEqual(coffees[0].Hash, coffees[1].Hash);
    }

    [Fact]
    public void Maps_type_and_status()
    {
        var rows = ReadCsv().Rows;

        Assert.Equal("AJUSTE", rows.Single(r => r.Description == "Ajuste Janeiro").Type);
        Assert.Equal(TransactionStatus.Pending, rows.Single(r => r.Description == "Conta a vencer").Status);
        Assert.Equal(TransactionStatus.Active, rows.Single(r => r.Description == "Salário").Status);
    }

    [Fact]
    public void Xlsx_reads_the_same_rows_as_csv()
    {
        using var csv = ImportFixture.CsvStream();
        using var xlsx = ImportFixture.XlsxStream();

        var fromCsv = ImportReader.Read(csv, "gastos.csv");
        var fromXlsx = ImportReader.Read(xlsx, "gastos.xlsx");

        Assert.Equal(fromCsv.Rows.Select(r => (r.OccurredOn, r.Description, r.SignedAmount, r.Type, r.Status)),
            fromXlsx.Rows.Select(r => (r.OccurredOn, r.Description, r.SignedAmount, r.Type, r.Status)));
    }

    [Theory]
    [InlineData("AJUSTE", CategoryKind.Adjustment)]
    [InlineData("Reserva", CategoryKind.Reserve)]
    [InlineData("INVESTIMENTO", CategoryKind.Investment)]
    [InlineData("MUSEU", CategoryKind.Excluded)]
    [InlineData("CONSÓRCIO", CategoryKind.Regular)]
    public void Type_decides_category_kind(string type, CategoryKind expected) =>
        Assert.Equal(expected, ImportReader.KindOf(type));
}
