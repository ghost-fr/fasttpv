using System.Text.Json;
using System.Text.Json.Nodes;

namespace FastTPV.Core.Data;

/// <summary>
/// The subset of appsettings.json FastTPV actually reads at startup.
/// Kept deliberately small and dependency-free (System.Text.Json only) rather than
/// pulling in Microsoft.Extensions.Configuration for a handful of values.
/// </summary>
public class RuntimeSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public decimal DefaultTaxRate { get; set; } = 0.10m;
    public string DefaultCurrency { get; set; } = "USD";
    public bool AutoMigrate { get; set; } = true;
    public int AutoLogoffMinutes { get; set; } = 15;
    public string ReceiptFormat { get; set; } = "80mm";
    public bool RequireLogin { get; set; } = true;
    public int MinPasswordLength { get; set; } = 8;

    /// <summary>
    /// The highest discount percentage (0-100) a Cashier can apply — on a line or on
    /// the whole ticket — without an Admin's password. Above this, ApprovalDialog is
    /// shown. Matches the "some discounts need manager approval" pattern most POS
    /// systems use, simplified to a single threshold rather than a discount catalog
    /// with a per-discount approval flag.
    /// </summary>
    public decimal MaxCashierDiscountPercent { get; set; } = 15m;

    /// <summary>
    /// Failed sign-in attempts (for one username) before the account is temporarily
    /// locked — see UserAccountService.AuthenticateAsync.
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked after hitting MaxFailedLoginAttempts.</summary>
    public int LockoutMinutes { get; set; } = 15;

    // --- Company / business identity (printed on receipts and shown in UI) ---
    public string BusinessName { get; set; } = "FastTPV";
    public string TradeName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string ReceiptHeader { get; set; } = string.Empty;
    public string ReceiptFooter { get; set; } = "Thank you for your purchase!";
    public string LogoPath { get; set; } = string.Empty;

    // Printer connection: "None" (default — text-file receipt only, nothing sent to
    // hardware), "Network" (raw ESC/POS over TCP to a thermal printer), or
    // "WindowsShell" (legacy: ask Windows to print the receipt file with whatever's
    // registered for .txt — for a regular document printer, not a thermal one).
    public string PrinterName { get; set; } = "Default Printer";
    public string PrinterConnection { get; set; } = "None";
    public string PrinterIpAddress { get; set; } = string.Empty;
    public int PrinterPort { get; set; } = 9100;
    public int PrinterCodePage { get; set; } = 16;
    public int PrinterConnectTimeoutMs { get; set; } = 3000;

    // How the printer's paper is cut after a receipt: "Standard" (GS V m — the
    // classic one-parameter form most ESC/POS-compatible printers use), "TwoParameter"
    // (GS V 66 n — some clone/older firmware expects this instead), or "None" (no
    // cutter — the customer tears the paper by hand).
    public string PrinterCutStyle { get; set; } = "Standard";

    public static readonly string DefaultConnectionString =
        "Server=localhost;Database=FastTPV;Uid=root;Pwd=your_password;Convert Zero Datetime=true;Allow Zero Datetime=true";

    /// <summary>
    /// Builds a multi-line company header suitable for receipts (name, address, tax id, phone).
    /// Empty fields are skipped so the header stays compact.
    /// </summary>
    public IEnumerable<string> GetReceiptHeaderLines()
    {
        if (!string.IsNullOrWhiteSpace(BusinessName))
            yield return BusinessName.Trim();

        if (!string.IsNullOrWhiteSpace(TradeName) &&
            !string.Equals(TradeName.Trim(), BusinessName.Trim(), StringComparison.OrdinalIgnoreCase))
            yield return TradeName.Trim();

        if (!string.IsNullOrWhiteSpace(TaxId))
            yield return $"Tax ID: {TaxId.Trim()}";

        if (!string.IsNullOrWhiteSpace(AddressLine1))
            yield return AddressLine1.Trim();

        if (!string.IsNullOrWhiteSpace(AddressLine2))
            yield return AddressLine2.Trim();

        var cityLine = string.Join(" ", new[] { PostalCode, City, Province }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim()));
        if (!string.IsNullOrWhiteSpace(cityLine))
            yield return cityLine;

        if (!string.IsNullOrWhiteSpace(Country))
            yield return Country.Trim();

        if (!string.IsNullOrWhiteSpace(Phone))
            yield return $"Tel: {Phone.Trim()}";

        if (!string.IsNullOrWhiteSpace(Email))
            yield return Email.Trim();

        if (!string.IsNullOrWhiteSpace(Website))
            yield return Website.Trim();

        if (!string.IsNullOrWhiteSpace(ReceiptHeader))
            yield return ReceiptHeader.Trim();
    }

    /// <summary>
    /// Loads settings from the given path. If the file doesn't exist, returns the
    /// documented defaults (matching appsettings.json.template) so the app still starts
    /// and reports a clear connection error instead of crashing on launch.
    /// </summary>
    public static RuntimeSettings Load(string path)
    {
        var settings = new RuntimeSettings { ConnectionString = DefaultConnectionString };

        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                if (root.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty("DefaultConnection", out var def))
                {
                    settings.ConnectionString = def.GetString() ?? settings.ConnectionString;
                }

                if (root.TryGetProperty("Database", out var database) &&
                    database.TryGetProperty("AutoMigrate", out var autoMigrate))
                {
                    settings.AutoMigrate = autoMigrate.GetBoolean();
                }

                // Legacy Application.Company / Name still supported as fallback
                if (root.TryGetProperty("Application", out var application))
                {
                    if (application.TryGetProperty("Company", out var company) &&
                        !string.IsNullOrWhiteSpace(company.GetString()))
                    {
                        settings.BusinessName = company.GetString()!;
                    }
                    else if (application.TryGetProperty("Name", out var name))
                    {
                        settings.BusinessName = name.GetString() ?? settings.BusinessName;
                    }
                }

                // Full Company section (preferred)
                if (root.TryGetProperty("Company", out var companySection))
                {
                    if (companySection.TryGetProperty("BusinessName", out var bn) && !string.IsNullOrWhiteSpace(bn.GetString()))
                        settings.BusinessName = bn.GetString()!;
                    if (companySection.TryGetProperty("TradeName", out var tn))
                        settings.TradeName = tn.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("TaxId", out var taxId))
                        settings.TaxId = taxId.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("AddressLine1", out var a1))
                        settings.AddressLine1 = a1.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("AddressLine2", out var a2))
                        settings.AddressLine2 = a2.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("City", out var city))
                        settings.City = city.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("PostalCode", out var pc))
                        settings.PostalCode = pc.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("Province", out var prov))
                        settings.Province = prov.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("Country", out var country))
                        settings.Country = country.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("Phone", out var phone))
                        settings.Phone = phone.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("Email", out var email))
                        settings.Email = email.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("Website", out var web))
                        settings.Website = web.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("ReceiptHeader", out var rh))
                        settings.ReceiptHeader = rh.GetString() ?? string.Empty;
                    if (companySection.TryGetProperty("ReceiptFooter", out var rf))
                        settings.ReceiptFooter = rf.GetString() ?? settings.ReceiptFooter;
                    if (companySection.TryGetProperty("LogoPath", out var logo))
                        settings.LogoPath = logo.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("POS", out var pos))
                {
                    if (pos.TryGetProperty("DefaultTaxRate", out var taxRate))
                        settings.DefaultTaxRate = taxRate.GetDecimal();

                    if (pos.TryGetProperty("DefaultCurrency", out var currency))
                        settings.DefaultCurrency = currency.GetString() ?? settings.DefaultCurrency;

                    if (pos.TryGetProperty("AutoLogoffMinutes", out var logoff) && logoff.TryGetInt32(out var logoffMinutes))
                        settings.AutoLogoffMinutes = logoffMinutes;

                    if (pos.TryGetProperty("MaxCashierDiscountPercent", out var maxDiscount))
                        settings.MaxCashierDiscountPercent = maxDiscount.GetDecimal();

                    if (pos.TryGetProperty("PrinterName", out var printer))
                        settings.PrinterName = printer.GetString() ?? settings.PrinterName;

                    if (pos.TryGetProperty("ReceiptFormat", out var format))
                        settings.ReceiptFormat = format.GetString() ?? settings.ReceiptFormat;

                    if (pos.TryGetProperty("PrinterConnection", out var connection))
                        settings.PrinterConnection = connection.GetString() ?? settings.PrinterConnection;

                    if (pos.TryGetProperty("PrinterIpAddress", out var ip))
                        settings.PrinterIpAddress = ip.GetString() ?? settings.PrinterIpAddress;

                    if (pos.TryGetProperty("PrinterPort", out var port) && port.TryGetInt32(out var portValue))
                        settings.PrinterPort = portValue;

                    if (pos.TryGetProperty("PrinterCodePage", out var codePage) && codePage.TryGetInt32(out var codePageValue))
                        settings.PrinterCodePage = codePageValue;

                    if (pos.TryGetProperty("PrinterCutStyle", out var cutStyle))
                        settings.PrinterCutStyle = cutStyle.GetString() ?? settings.PrinterCutStyle;

                    if (pos.TryGetProperty("PrinterConnectTimeoutMs", out var timeout) && timeout.TryGetInt32(out var timeoutValue))
                        settings.PrinterConnectTimeoutMs = timeoutValue;
                }

                if (root.TryGetProperty("Security", out var security))
                {
                    if (security.TryGetProperty("RequireLogin", out var requireLogin))
                        settings.RequireLogin = requireLogin.GetBoolean();

                    if (security.TryGetProperty("MinPasswordLength", out var minLength) && minLength.TryGetInt32(out var minLen))
                        settings.MinPasswordLength = minLen;

                    if (security.TryGetProperty("MaxFailedLoginAttempts", out var maxAttempts) && maxAttempts.TryGetInt32(out var maxAttemptsValue))
                        settings.MaxFailedLoginAttempts = maxAttemptsValue;

                    if (security.TryGetProperty("LockoutMinutes", out var lockoutMinutes) && lockoutMinutes.TryGetInt32(out var lockoutMinutesValue))
                        settings.LockoutMinutes = lockoutMinutesValue;
                }
            }
            catch (Exception)
            {
                // Malformed appsettings.json — fall back to defaults rather than crash on launch.
            }
        }

        // Production secret handling: an environment variable, when set, always wins
        // over whatever appsettings.json contains (or the built-in placeholder above).
        // This lets a real deployment keep the actual DB password out of a file on
        // disk entirely — set FASTTPV_CONNECTION_STRING at the OS/service level
        // instead of editing appsettings.json. Checked after the file is parsed (or
        // even if parsing failed/the file is missing), so it's the final word either way.
        var envConnectionString = Environment.GetEnvironmentVariable("FASTTPV_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            settings.ConnectionString = envConnectionString;
        }

        return settings;
    }

    /// <summary>
    /// Writes printer-related fields back into the POS section of the settings file.
    /// </summary>
    public void SavePrinterSettings(string path)
    {
        var root = LoadOrCreateRoot(path);

        if (root["POS"] is not JsonObject pos)
        {
            pos = new JsonObject();
            root["POS"] = pos;
        }

        pos["ReceiptFormat"] = ReceiptFormat;
        pos["PrinterName"] = PrinterName;
        pos["PrinterConnection"] = PrinterConnection;
        pos["PrinterIpAddress"] = PrinterIpAddress;
        pos["PrinterPort"] = PrinterPort;
        pos["PrinterCodePage"] = PrinterCodePage;
        pos["PrinterCutStyle"] = PrinterCutStyle;
        pos["PrinterConnectTimeoutMs"] = PrinterConnectTimeoutMs;

        WriteRoot(path, root);
    }

    /// <summary>
    /// Writes company / business identity fields back into the Company section
    /// (and keeps Application.Company in sync for backward compatibility).
    /// </summary>
    public void SaveCompanySettings(string path)
    {
        var root = LoadOrCreateRoot(path);

        if (root["Company"] is not JsonObject company)
        {
            company = new JsonObject();
            root["Company"] = company;
        }

        company["BusinessName"] = BusinessName;
        company["TradeName"] = TradeName;
        company["TaxId"] = TaxId;
        company["AddressLine1"] = AddressLine1;
        company["AddressLine2"] = AddressLine2;
        company["City"] = City;
        company["PostalCode"] = PostalCode;
        company["Province"] = Province;
        company["Country"] = Country;
        company["Phone"] = Phone;
        company["Email"] = Email;
        company["Website"] = Website;
        company["ReceiptHeader"] = ReceiptHeader;
        company["ReceiptFooter"] = ReceiptFooter;
        company["LogoPath"] = LogoPath;

        // Keep legacy key in sync
        if (root["Application"] is not JsonObject application)
        {
            application = new JsonObject();
            root["Application"] = application;
        }
        application["Company"] = BusinessName;

        WriteRoot(path, root);
    }

    private static JsonObject LoadOrCreateRoot(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
            }
            catch (Exception)
            {
                return new JsonObject();
            }
        }
        return new JsonObject();
    }

    private static void WriteRoot(string path, JsonObject root)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
