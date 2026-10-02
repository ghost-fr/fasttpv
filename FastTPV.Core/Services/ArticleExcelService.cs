using System.Globalization;
using ClosedXML.Excel;
using FastTPV.Core.Models;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Excel (.xlsx) export and import of the article catalogue.
/// All database work is delegated to the existing ArticleService / StockMovementService /
/// CategoryService; this class only owns the file format. Parsing (<see cref="Parse"/>) is
/// pure - no database - so it can be unit-tested without MySQL.
///
/// Import rules (upsert by Code):
///  - Required columns: Code, Name, Price. Optional: Description, CostPrice, StockLevel,
///    MinimumStock, Category, PricingMode, IsActive. Column order is free; header names are
///    case-insensitive.
///  - Optional column absent OR cell blank => on update the existing value is left alone,
///    on insert a default is used. (So an import cannot blank out a Description.)
///  - StockLevel in the file is the counted level at import time; the difference is applied
///    as an atomic delta and recorded as a StockMovement ("Excel import").
///  - Invalid rows are skipped and reported; valid rows are still imported.
/// </summary>
public sealed class ArticleExcelService
{
    public static readonly string[] Columns =
    {
        "Code", "Name", "Description", "Price", "CostPrice",
        "StockLevel", "MinimumStock", "Category", "PricingMode", "IsActive"
    };

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["barcode"] = "Code", ["stock"] = "StockLevel", ["cost"] = "CostPrice",
        ["minstock"] = "MinimumStock", ["minimum"] = "MinimumStock", ["active"] = "IsActive"
    };

    private static readonly ILogger Logger = Log.ForContext<ArticleExcelService>();

    private readonly ArticleService _articles;
    private readonly StockMovementService _movements;
    private readonly CategoryService _categories;

    public ArticleExcelService(ArticleService articles, StockMovementService movements, CategoryService categories)
    {
        _articles = articles;
        _movements = movements;
        _categories = categories;
    }

    // ------------------------------------------------------------------ export

    /// <summary>
    /// Writes every article (active AND inactive) to <paramref name="path"/> as .xlsx.
    /// Written to a temp file first so a failure never leaves a half-written file behind.
    /// Throws if the catalogue cannot be read, rather than silently exporting an empty file.
    /// </summary>
    /// <returns>Number of articles exported.</returns>
    public async Task<int> ExportAsync(string path)
    {
        var all = await _articles.GetAllIncludingInactiveAsync();
        if (all.Count == 0)
            throw new InvalidOperationException("There are no products to export (or the database could not be read - check the log).");

        var tmp = path + ".tmp";
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                WriteWorkbook(all, fs);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        Logger.Information("Exported {Count} articles to {Path}", all.Count, path);
        return all.Count;
    }

    /// <summary>Renders the catalogue to an .xlsx stream (no database access).</summary>
    public static void WriteWorkbook(IEnumerable<Article> articles, Stream output)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Articles");

        for (int c = 0; c < Columns.Length; c++)
        {
            var h = ws.Cell(1, c + 1);
            h.SetValue(Columns[c]);
            h.Style.Font.Bold = true;
            h.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Code is stored as TEXT so barcodes keep leading zeros and never turn into 8.41E+12.
        ws.Column(1).Style.NumberFormat.Format = "@";

        int r = 2;
        foreach (var a in articles)
        {
            ws.Cell(r, 1).SetValue(a.Code);
            ws.Cell(r, 2).SetValue(a.Name);
            ws.Cell(r, 3).SetValue(a.Description ?? string.Empty);
            ws.Cell(r, 4).SetValue(a.Price);
            ws.Cell(r, 5).SetValue(a.CostPrice);
            ws.Cell(r, 6).SetValue(a.StockLevel);
            ws.Cell(r, 7).SetValue(a.MinimumStock);
            ws.Cell(r, 8).SetValue(a.Category ?? string.Empty);
            ws.Cell(r, 9).SetValue(string.IsNullOrWhiteSpace(a.PricingMode) ? "Fixed" : a.PricingMode);
            ws.Cell(r, 10).SetValue(a.IsActive);
            r++;
        }

        ws.Range(2, 4, Math.Max(2, r - 1), 5).Style.NumberFormat.Format = "0.00";
        var widths = new[] { 18, 36, 36, 11, 11, 11, 13, 20, 13, 10 };
        for (int c = 0; c < widths.Length; c++) ws.Column(c + 1).Width = widths[c];
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();

        wb.SaveAs(output);
    }

    // ------------------------------------------------------------------ parse (pure)

    public sealed class ParsedRow
    {
        public int RowNumber { get; init; }
        public string Code { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public decimal? CostPrice { get; set; }
        public int? StockLevel { get; set; }
        public int? MinimumStock { get; set; }
        public string? Category { get; set; }
        public string? PricingMode { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class ParsedWorkbook
    {
        public List<ParsedRow> Rows { get; } = new();
        public List<string> Warnings { get; } = new();
        public string? FatalError { get; set; }
    }

    /// <summary>
    /// Reads and validates an .xlsx stream. Never touches the database. Row problems are
    /// recorded on the row (<see cref="ParsedRow.Error"/>); only unusable files set FatalError.
    /// </summary>
    public static ParsedWorkbook Parse(Stream input)
    {
        var result = new ParsedWorkbook();
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(input);
        }
        catch (Exception ex)
        {
            result.FatalError = "The file is not a readable .xlsx workbook (" + ex.Message + ").";
            return result;
        }

        using (wb)
        {
            var ws = wb.Worksheets.TryGetWorksheet("Articles", out var named) ? named : wb.Worksheets.FirstOrDefault();
            if (ws is null) { result.FatalError = "The workbook has no sheets."; return result; }

            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastCol == 0 || lastRow < 1) { result.FatalError = "The sheet is empty."; return result; }

            var colOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= lastCol; c++)
            {
                var h = ws.Cell(1, c).GetString().Trim();
                if (h.Length == 0) continue;
                if (HeaderAliases.TryGetValue(h, out var canonical)) h = canonical;
                colOf.TryAdd(h, c);
            }

            var missing = new[] { "Code", "Name", "Price" }.Where(h => !colOf.ContainsKey(h)).ToList();
            if (missing.Count > 0)
            {
                result.FatalError = "Missing required column(s): " + string.Join(", ", missing) +
                                    ". Expected headers in row 1: " + string.Join(", ", Columns) + ".";
                return result;
            }

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int numericCodes = 0;

            for (int r = 2; r <= lastRow; r++)
            {
                if (Enumerable.Range(1, lastCol).All(c => ws.Cell(r, c).IsEmpty())) continue; // blank row

                var row = new ParsedRow { RowNumber = r };
                result.Rows.Add(row);

                IXLCell Cell(string h) => ws.Cell(r, colOf[h]);
                bool Has(string h) => colOf.ContainsKey(h);

                var codeCell = Cell("Code");
                if (codeCell.Value.IsNumber) numericCodes++;
                row.Code = CellText(codeCell) ?? string.Empty;
                row.Name = CellText(Cell("Name")) ?? string.Empty;

                if (row.Code.Length == 0) { row.Error = "Code (barcode) is empty."; continue; }
                if (seen.TryGetValue(row.Code, out var firstRow))
                {
                    row.Error = $"Duplicate Code in this file (first seen on row {firstRow}); only the first was used.";
                    continue;
                }
                seen[row.Code] = r;

                if (row.Name.Length == 0) { row.Error = "Name is empty."; continue; }

                if (!TryDecimal(Cell("Price"), true, out var price, out var perr) || price is null)
                { row.Error = "Price: " + (perr ?? "required."); continue; }
                row.Price = price.Value;

                if (Has("CostPrice"))
                {
                    if (!TryDecimal(Cell("CostPrice"), false, out var cost, out var cerr)) { row.Error = "CostPrice: " + cerr; continue; }
                    row.CostPrice = cost;
                }
                if (Has("StockLevel"))
                {
                    if (!TryInt(Cell("StockLevel"), out var stock, out var serr)) { row.Error = "StockLevel: " + serr; continue; }
                    row.StockLevel = stock;
                }
                if (Has("MinimumStock"))
                {
                    if (!TryInt(Cell("MinimumStock"), out var min, out var merr)) { row.Error = "MinimumStock: " + merr; continue; }
                    row.MinimumStock = min;
                }
                if (Has("Description")) row.Description = CellText(Cell("Description"));
                if (Has("Category")) row.Category = CellText(Cell("Category"));
                if (Has("PricingMode"))
                {
                    var pm = CellText(Cell("PricingMode"));
                    if (pm is not null)
                    {
                        if (pm.Equals("Fixed", StringComparison.OrdinalIgnoreCase)) row.PricingMode = "Fixed";
                        else if (pm.Equals("OpenPrice", StringComparison.OrdinalIgnoreCase)) row.PricingMode = "OpenPrice";
                        else { row.Error = $"PricingMode '{pm}' is not Fixed or OpenPrice."; continue; }
                    }
                }
                if (Has("IsActive"))
                {
                    if (!TryBool(Cell("IsActive"), out var active, out var aerr)) { row.Error = "IsActive: " + aerr; continue; }
                    row.IsActive = active;
                }

                // Shared business rules (lengths, negatives, DECIMAL range...).
                row.Error = ArticleService.Validate(ToDraft(row));
            }

            if (numericCodes > 0)
                result.Warnings.Add($"{numericCodes} barcode(s) were stored as numbers in Excel. Leading zeros cannot be recovered - " +
                                    "format the Code column as Text before typing barcodes.");
        }
        return result;
    }

    private static Article ToDraft(ParsedRow r) => new()
    {
        Code = r.Code, Name = r.Name, Price = r.Price,
        Description = r.Description ?? string.Empty,
        CostPrice = r.CostPrice ?? 0m,
        StockLevel = r.StockLevel ?? 0,
        MinimumStock = r.MinimumStock ?? 0,
        Category = r.Category ?? string.Empty,
        PricingMode = r.PricingMode ?? "Fixed",
        IsActive = r.IsActive ?? true
    };

    // ------------------------------------------------------------------ import (DB)

    /// <summary>
    /// Upserts the rows of an .xlsx stream by Code. Never throws for bad data: problems are
    /// reported in the returned summary. Each row is its own unit of work, so one bad row
    /// cannot block the others (and a crash mid-import leaves earlier rows safely applied;
    /// re-running the same file is idempotent).
    /// </summary>
    /// <param name="userId">Recorded on stock movements created by the import.</param>
    public async Task<ArticleImportSummary> ImportAsync(Stream input, int userId, IProgress<int>? progress = null)
    {
        var summary = new ArticleImportSummary();
        var parsed = Parse(input);
        if (parsed.FatalError is not null)
        {
            summary.FatalError = parsed.FatalError;
            Logger.Warning("Import rejected: {Error}", parsed.FatalError);
            return summary;
        }
        summary.Warnings.AddRange(parsed.Warnings);

        int done = 0;
        foreach (var row in parsed.Rows)
        {
            progress?.Report(++done);
            if (row.Error is not null)
            {
                summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code, row.Error));
                Logger.Warning("Import row {Row} skipped: {Error}", row.RowNumber, row.Error);
                continue;
            }

            try
            {
                await ApplyRowAsync(row, userId, summary);
            }
            catch (DuplicateCodeException ex)
            {
                summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code, ex.Message));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Import row {Row} ({Code}) failed", row.RowNumber, row.Code);
                summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code, "Unexpected error: " + ex.Message));
            }
        }

        Logger.Information("Import finished: {Added} added, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped",
            summary.Added, summary.Updated, summary.Unchanged, summary.Skipped);
        return summary;
    }

    private async Task ApplyRowAsync(ParsedRow row, int userId, ArticleImportSummary summary)
    {
        if (!string.IsNullOrWhiteSpace(row.Category))
            await _categories.EnsureExistsAsync(row.Category!);

        var existing = await _articles.GetByCodeAsync(row.Code); // includes inactive articles

        if (existing is null)
        {
            var draft = ToDraft(row);
            var id = await _articles.AddAsync(draft);
            if (id <= 0)
            {
                summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code, "Could not be saved (see log)."));
                return;
            }
            if (draft.StockLevel > 0)
                await RecordStockAsync(draft, draft.StockLevel, "In", userId);
            summary.Added++;
            return;
        }

        bool changed = false;
        void Set<T>(T? incoming, T current, Action<T> apply) where T : struct
        {
            if (incoming is { } v && !EqualityComparer<T>.Default.Equals(v, current)) { apply(v); changed = true; }
        }
        void SetText(string? incoming, string current, Action<string> apply)
        {
            if (incoming is not null && !string.Equals(incoming, current, StringComparison.Ordinal)) { apply(incoming); changed = true; }
        }

        if (!string.Equals(row.Name, existing.Name, StringComparison.Ordinal)) { existing.Name = row.Name; changed = true; }
        if (row.Price != existing.Price) { existing.Price = row.Price; changed = true; }
        Set(row.CostPrice, existing.CostPrice, v => existing.CostPrice = v);
        Set(row.MinimumStock, existing.MinimumStock, v => existing.MinimumStock = v);
        Set(row.IsActive, existing.IsActive, v => existing.IsActive = v);
        SetText(row.Description, existing.Description, v => existing.Description = v);
        SetText(row.Category, existing.Category, v => existing.Category = v);
        SetText(row.PricingMode, existing.PricingMode, v => existing.PricingMode = v);

        int stockDelta = row.StockLevel is { } target ? target - existing.StockLevel : 0;

        if (!changed && stockDelta == 0)
        {
            summary.Unchanged++;
            return;
        }

        if (changed && !await _articles.UpdateAsync(existing))
        {
            summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code, "Could not be updated (see log)."));
            return;
        }

        if (stockDelta != 0)
        {
            var ok = await _movements.AdjustStockWithReasonAsync(existing.Id, stockDelta, "Excel import", userId);
            if (!ok)
            {
                summary.Errors.Add(new ImportRowError(row.RowNumber, row.Code,
                    "Fields were updated but the stock change could not be applied (would make stock negative, or database error)."));
                return;
            }
        }
        summary.Updated++;
    }

    private Task RecordStockAsync(Article a, int qty, string type, int userId) =>
        _movements.RecordAsync(new StockMovement
        {
            ArticleId = a.Id, ArticleCode = a.Code, ArticleName = a.Name,
            Quantity = qty, MovementType = type, Reason = "Initial stock (Excel import)",
            UserId = userId, CreatedAt = DateTime.Now
        });

    // ------------------------------------------------------------------ cell helpers

    private static string? CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var v = cell.Value;
        string text;
        if (v.IsNumber)
        {
            var d = (decimal)v.GetNumber();
            text = d == decimal.Truncate(d) ? d.ToString("0", CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture);
        }
        else if (v.IsBoolean) text = v.GetBoolean() ? "TRUE" : "FALSE";
        else if (v.IsDateTime) text = v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        else text = cell.GetString();
        text = text.Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Blank cell => ok with null value. Number cell or text such as "1,50" / "1.234,56" / "12.5".</summary>
    private static bool TryDecimal(IXLCell cell, bool required, out decimal? value, out string? error)
    {
        value = null; error = null;
        if (cell.IsEmpty())
        {
            if (!required) return true;     // optional and blank = "not provided"
            error = "required."; return false;
        }
        decimal d;
        if (cell.Value.IsNumber) d = (decimal)cell.Value.GetNumber();
        else
        {
            var t = CellText(cell);
            if (t is null) { error = "required."; return false; }
            if (!TryParseDecimalText(t, out d)) { error = $"'{t}' is not a valid number."; return false; }
        }
        value = Math.Round(d, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    internal static bool TryParseDecimalText(string t, out decimal d)
    {
        t = t.Replace("€", "").Replace(" ", "").Trim();
        int comma = t.LastIndexOf(','), dot = t.LastIndexOf('.');
        if (comma >= 0 && dot >= 0) // both present: the later one is the decimal separator
            t = comma > dot ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
        else if (comma >= 0)        // only comma: decimal comma (Spanish style)
            t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out d);
    }

    private static bool TryInt(IXLCell cell, out int? value, out string? error)
    {
        value = null; error = null;
        if (cell.IsEmpty()) return true; // optional: blank = not provided
        if (!TryDecimal(cell, false, out var d, out error) || d is null) return false;
        if (d != decimal.Truncate(d)) { error = "must be a whole number."; return false; }
        if (d > int.MaxValue || d < int.MinValue) { error = "is out of range."; return false; }
        value = (int)d;
        return true;
    }

    private static bool TryBool(IXLCell cell, out bool? value, out string? error)
    {
        value = null; error = null;
        if (cell.IsEmpty()) return true;
        if (cell.Value.IsBoolean) { value = cell.Value.GetBoolean(); return true; }
        var t = CellText(cell)?.ToLowerInvariant();
        switch (t)
        {
            case "true": case "1": case "yes": case "y": case "si": case "sí": value = true; return true;
            case "false": case "0": case "no": case "n": value = false; return true;
            default: error = $"'{t}' is not TRUE/FALSE."; return false;
        }
    }
}
