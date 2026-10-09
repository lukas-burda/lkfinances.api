using LkFinances.Core.Domain;
using LkFinances.Core.Import;

namespace LkFinances.Api.Tests;

public class SpreadsheetReaderTests
{
    private static SpreadsheetData Read()
    {
        using var stream = SpreadsheetFixture.Build();
        return SpreadsheetReader.Read(stream);
    }

    [Fact]
    public void Reads_rows_rounding_amounts_and_skipping_zero_lines()
    {
        var data = Read();

        Assert.Equal(11, data.Rows.Count);
        Assert.Equal(1, data.SkippedRows);
        Assert.Equal(-1081.86m, data.Rows.Single(r => r.Description == "Mercado").SignedAmount);
        Assert.Equal(new DateOnly(2025, 12, 30), data.Rows.Single(r => r.Description == "Salário dezembro").OccurredOn);
    }

    [Fact]
    public void Row_with_value_but_no_description_is_kept_because_the_resumo_counts_it()
    {
        var row = Read().Rows.Single(r => r.Description == SpreadsheetReader.MissingDescription);

        Assert.Equal(50m, row.SignedAmount);
        Assert.Equal("RESERVA", row.Type);
    }

    [Fact]
    public void Identical_rows_get_distinct_hashes()
    {
        var coffees = Read().Rows.Where(r => r.Description == "Café").ToList();

        Assert.Equal(2, coffees.Count);
        Assert.NotEqual(coffees[0].Hash, coffees[1].Hash);
    }

    [Fact]
    public void Maps_type_and_pending_status()
    {
        var rows = Read().Rows;

        Assert.Equal("AJUSTE", rows.Single(r => r.Description == "Ajuste Janeiro").Type);
        Assert.Equal(TransactionStatus.Pending, rows.Single(r => r.Description == "Conta a vencer").Status);
        Assert.Equal(TransactionStatus.Active, rows.Single(r => r.Description == "Salário").Status);
    }

    [Fact]
    public void Reads_real_balances_that_are_filled()
    {
        var real = Read().RealBalances;

        Assert.Equal(1500m, real[Bucket.Surplus]);
        Assert.False(real.ContainsKey(Bucket.Reserve));
    }

    [Theory]
    [InlineData("AJUSTE", CategoryKind.Adjustment)]
    [InlineData("Reserva", CategoryKind.Reserve)]
    [InlineData("INVESTIMENTO", CategoryKind.Investment)]
    [InlineData("MUSEU", CategoryKind.Excluded)]
    [InlineData("MUSEU-EFETIVO", CategoryKind.Regular)]
    [InlineData("CONSÓRCIO", CategoryKind.Regular)]
    public void Spreadsheet_type_decides_category_kind(string type, CategoryKind expected) =>
        Assert.Equal(expected, SpreadsheetReader.KindOf(type));
}
