using ClosedXML.Excel;
using FastTPV.Core.Models;
using FastTPV.Core.Services;
using Xunit;

namespace FastTPV.Tests;

/// <summary>
/// Database-free tests for the Excel parser, export format and article validation.
/// (Upsert/stock paths need a MySQL instance - see README-CHANGES.md "How to test".)
/// </summary>
public class ArticleExcelTests
{
    private static MemoryStream Workbook(string[] header, params object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Articles");
        for (int c = 0; c < header.Length; c++) ws.Cell(1, c + 1).SetValue(header[c]);
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                var v = rows[r][c];
                if (v is null) continue;
                var cell = ws.Cell(r + 2, c + 1);
                switch (v)
                {
                    case string s: cell.SetValue(s); break;
                    case int i: cell.SetValue(i); break;
                    case double d: cell.SetValue(d); break;
                    case bool b: cell.SetValue(b); break;
                }
            }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static readonly string[] Full =
        { "Code", "Name", "Description", "Price", "CostPrice", "StockLevel", "MinimumStock", "Category", "PricingMode", "IsActive" };

    [Fact]
    public void Parse_reads_all_columns()
    {
        var parsed = ArticleExcelService.Parse(Workbook(Full,
            new object?[] { "8410000000017", "Milk 1L", "Whole", 1.25, 0.9, 40, 5, "Dairy", "Fixed", true }));

        Assert.Null(parsed.FatalError);
        var row = Assert.Single(parsed.Rows);
        Assert.Null(row.Error);
        Assert.Equal("8410000000017", row.Code);
        Assert.Equal(1.25m, row.Price);
        Assert.Equal(40, row.StockLevel);
        Assert.Equal("Dairy", row.Category);
        Assert.True(row.IsActive);
    }

    [Fact]
    public void Parse_missing_required_column_is_fatal()
    {
        var parsed = ArticleExcelService.Parse(Workbook(new[] { "Code", "Name" }, new object?[] { "A1", "Thing" }));
        Assert.NotNull(parsed.FatalError);
        Assert.Contains("Price", parsed.FatalError);
    }

    [Fact]
    public void Parse_skips_bad_rows_but_keeps_good_ones()
    {
        var parsed = ArticleExcelService.Parse(Workbook(Full,
            new object?[] { "A1", "Good", null, 2.0, null, 5, null, null, null, null },
            new object?[] { "A2", "Negative stock", null, 2.0, null, -3, null, null, null, null },
            new object?[] { "A3", "Bad price", null, "abc", null, 1, null, null, null, null },
            new object?[] { "A4", "Bad mode", null, 1.0, null, 1, null, null, "Weird", null },
            new object?[] { "", "No code", null, 1.0, null, 1, null, null, null, null }));

        Assert.Single(parsed.Rows, r => r.Error is null);
        Assert.Equal(4, parsed.Rows.Count(r => r.Error is not null));
    }

    [Fact]
    public void Parse_flags_duplicate_code_within_file_keeping_first()
    {
        var parsed = ArticleExcelService.Parse(Workbook(Full,
            new object?[] { "DUP", "First", null, 1.0, null, null, null, null, null, null },
            new object?[] { "dup", "Second", null, 9.0, null, null, null, null, null, null }));

        Assert.Null(parsed.Rows[0].Error);
        Assert.Contains("Duplicate", parsed.Rows[1].Error);
    }

    [Fact]
    public void Same_name_different_codes_both_valid()
    {
        var parsed = ArticleExcelService.Parse(Workbook(Full,
            new object?[] { "100", "T-shirt", null, 10.0, null, null, null, null, null, null },
            new object?[] { "101", "T-shirt", null, 12.0, null, null, null, null, null, null }));

        Assert.All(parsed.Rows, r => Assert.Null(r.Error));
    }

    [Fact]
    public void Numeric_barcode_cells_produce_a_warning()
    {
        var parsed = ArticleExcelService.Parse(Workbook(Full,
            new object?[] { 8410000000017d, "Numeric code", null, 1.0, null, null, null, null, null, null }));

        Assert.Equal("8410000000017", parsed.Rows[0].Code);
        Assert.Single(parsed.Warnings);
    }

    [Fact]
    public void Absent_optional_columns_mean_not_provided()
    {
        var parsed = ArticleExcelService.Parse(Workbook(new[] { "Code", "Name", "Price" },
            new object?[] { "X1", "Only basics", 3.5 }));

        var row = Assert.Single(parsed.Rows);
        Assert.Null(row.StockLevel);
        Assert.Null(row.IsActive);
        Assert.Null(row.Category);
    }

    [Theory]
    [InlineData("1,50", 1.50)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("12.5", 12.5)]
    [InlineData("3 €", 3)]
    public void Decimal_text_handles_spanish_and_english_formats(string text, double expected)
    {
        Assert.True(ArticleExcelService.TryParseDecimalText(text, out var d));
        Assert.Equal((decimal)expected, d);
    }

    [Fact]
    public void Export_then_parse_round_trips_leading_zeros_and_inactive_flag()
    {
        var articles = new[]
        {
            new Article { Code = "0012345", Name = "Zero-padded", Price = 2.5m, StockLevel = 3, IsActive = true, Category = "Misc" },
            new Article { Code = "ZZ-9", Name = "Retired", Price = 1m, IsActive = false, PricingMode = "OpenPrice" }
        };
        using var ms = new MemoryStream();
        ArticleExcelService.WriteWorkbook(articles, ms);
        ms.Position = 0;

        var parsed = ArticleExcelService.Parse(ms);

        Assert.Null(parsed.FatalError);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal("0012345", parsed.Rows[0].Code);
        Assert.Empty(parsed.Warnings);
        Assert.False(parsed.Rows[1].IsActive);
        Assert.Equal("OpenPrice", parsed.Rows[1].PricingMode);
    }

    [Fact]
    public void Validate_enforces_business_rules()
    {
        Assert.NotNull(ArticleService.Validate(new Article { Code = "", Name = "x" }));
        Assert.NotNull(ArticleService.Validate(new Article { Code = "a", Name = "" }));
        Assert.NotNull(ArticleService.Validate(new Article { Code = "a", Name = "x", Price = -1 }));
        Assert.NotNull(ArticleService.Validate(new Article { Code = "a", Name = "x", Price = 100_000_000m }));
        Assert.NotNull(ArticleService.Validate(new Article { Code = "a", Name = "x", PricingMode = "Nope" }));
        Assert.Null(ArticleService.Validate(new Article { Code = "a", Name = "x", Price = 0m, PricingMode = "OpenPrice" }));
    }
}
