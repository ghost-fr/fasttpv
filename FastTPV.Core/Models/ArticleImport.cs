namespace FastTPV.Core.Models;

/// <summary>One problem found while importing a spreadsheet row.</summary>
/// <param name="Row">1-based Excel row number (header is row 1).</param>
/// <param name="Code">Barcode on that row, if one could be read.</param>
/// <param name="Message">Human-readable reason the row was skipped.</param>
public sealed record ImportRowError(int Row, string Code, string Message);

/// <summary>Outcome of an Excel import, shown to the user and written to the audit log.</summary>
public sealed class ArticleImportSummary
{
    public int Added { get; set; }
    public int Updated { get; set; }
    /// <summary>Rows that matched an existing article and changed nothing.</summary>
    public int Unchanged { get; set; }
    public int Skipped => Errors.Count;
    public List<ImportRowError> Errors { get; } = new();
    /// <summary>Non-fatal notes (e.g. numeric barcodes that may have lost leading zeros).</summary>
    public List<string> Warnings { get; } = new();
    /// <summary>Set when the file as a whole could not be used (wrong sheet, missing columns...).</summary>
    public string? FatalError { get; set; }

    public bool Succeeded => FatalError is null;

    public string ToText(int maxErrors = 25)
    {
        if (FatalError is not null) return "Import failed: " + FatalError;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Added: {Added}   Updated: {Updated}   Unchanged: {Unchanged}   Skipped: {Skipped}");
        foreach (var e in Errors.Take(maxErrors))
            sb.AppendLine($"  Row {e.Row}{(string.IsNullOrEmpty(e.Code) ? "" : $" ({e.Code})")}: {e.Message}");
        if (Errors.Count > maxErrors)
            sb.AppendLine($"  ...and {Errors.Count - maxErrors} more (see log).");
        foreach (var w in Warnings) sb.AppendLine("Note: " + w);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Thrown by ArticleService when a barcode (Code) is already used by a different article.
/// The message is written to be shown directly to the cashier/admin.
/// </summary>
public sealed class DuplicateCodeException : Exception
{
    public string Code { get; }
    public DuplicateCodeException(string code, string message) : base(message) => Code = code;
}
