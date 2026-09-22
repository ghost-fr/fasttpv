using System.Diagnostics;
using System.Text;
using FastTPV.Core.Models;
using Serilog;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Writes a text-file receipt (always, as a durable local record — also what you'd
/// use to reprint one manually) and, depending on POS.PrinterConnection in
/// appsettings.json, attempts to actually print it:
///
/// "None" — text file only. The default: no printer configured, so no
/// attempt is made and checkout never waits on one.
/// "Network" — raw ESC/POS bytes sent over TCP to
/// POS.PrinterIpAddress:PrinterPort. This is the real thermal-
/// printer path — see EscPosBuilder for exactly what's sent.
/// "WindowsShell" — legacy fallback: ask Windows to print the text file with
/// whatever's registered as the default handler for .txt.
///
/// A failed print attempt never blocks or undoes an already-completed sale.
/// Company header/footer come from RuntimeSettings (Company section).
/// </summary>
public sealed class ReceiptService
{
    private static readonly ILogger Logger = Log.ForContext<ReceiptService>();
    private readonly NetworkPrinterClient _networkPrinter = new();

    public async Task<string> CreateAndPrintAsync(Sale sale)
    {
        var settings = AppRuntime.Settings;
        var width = ReceiptWidth(settings.ReceiptFormat);

        var path = WriteTextReceipt(sale, settings, width);

        switch (settings.PrinterConnection)
        {
            case "Network":
                await PrintViaNetworkAsync(sale, width);
                break;
            case "WindowsShell":
                PrintViaWindowsShell(path, settings.PrinterName);
                break;
            // "None" (default) and anything unrecognized: text file only
        }

        return path;
    }

    /// <summary>
    /// Sends a small test ticket straight to a network printer using the given
    /// candidate settings. Used by PrinterSettingsWindow's "Send test print" button.
    /// </summary>
    public async Task<(bool Sent, string Message)> SendTestPrintAsync(
        string ipAddress, int port, int codePage, string cutStyle, int connectTimeoutMs,
        string businessName, string receiptFormat)
    {
        var width = ReceiptWidth(receiptFormat);
        var encoding = GetReceiptEncoding();

        var builder = new EscPosBuilder(encoding)
            .SelectCodePage(codePage)
            .Align(TextAlign.Center)
            .Bold(true)
            .DoubleSize(true)
            .Line(string.IsNullOrWhiteSpace(businessName) ? "FastTPV" : businessName)
            .DoubleSize(false)
            .Bold(false)
            .Line("*** TEST PRINT ***")
            .Align(TextAlign.Left)
            .Line(new string('-', width))
            .Line($"Code page: {codePage}")
            .Line($"Cut style: {cutStyle}")
            .Line($"Paper width: {receiptFormat}")
            .Line(new string('-', width))
            .Line("If the line below reads correctly, this code")
            .Line("page is right for your printer:")
            .Line(" áéíóúñ¿¡ — ÁÉÍÓÚÑ")
            .Line(new string('-', width))
            .Line(DateTime.Now.ToString("g"))
            .Feed(3)
            .Cut(EscPosBuilder.ParseCutStyle(cutStyle));

        var sent = await _networkPrinter.SendAsync(ipAddress, port, builder.ToArray(), connectTimeoutMs);

        return sent
            ? (true, "Test page sent. Check the printout: do the accented characters look right, " +
                     "and did the paper cut? Adjust the code page or cut style above and try again if not.")
            : (false, $"Could not reach a printer at {ipAddress}:{port}. Check that the IP is correct, " +
                      "the printer is powered on and on the same network, and that nothing (like a firewall) " +
                      "is blocking that port.");
    }

    /// <summary>
    /// Pulses the printer's cash-drawer port (see EscPosBuilder.KickDrawer for the
    /// exact command). Only meaningful when POS.PrinterConnection is "Network" and
    /// a drawer is actually wired to the printer's RJ11/RJ12 port — harmless
    /// no-op-from-the-user's-perspective otherwise (the send just won't do anything
    /// visible). Used by CashDrawerWindow's "Kick drawer" button and, optionally,
    /// after a Cash-tendered sale completes.
    /// </summary>
    public async Task<bool> KickDrawerAsync()
    {
        var settings = AppRuntime.Settings;

        if (settings.PrinterConnection != "Network")
        {
            Logger.Information("Drawer kick requested but PrinterConnection is not \"Network\" — nothing to send to.");
            return false;
        }

        var builder = new EscPosBuilder(Encoding.ASCII).KickDrawer();
        var sent = await _networkPrinter.SendAsync(
            settings.PrinterIpAddress, settings.PrinterPort, builder.ToArray(), settings.PrinterConnectTimeoutMs);

        if (!sent)
        {
            Logger.Warning("Could not reach network printer at {Ip}:{Port} to kick the cash drawer",
                settings.PrinterIpAddress, settings.PrinterPort);
        }

        return sent;
    }

    private static int ReceiptWidth(string receiptFormat) => receiptFormat == "58mm" ? 32 : 48;

    private static Encoding GetReceiptEncoding()
    {
        try
        {
            return Encoding.GetEncoding(1252);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not load Windows-1252 encoding for receipt printing; " +
                "falling back to ASCII (accented characters will not print correctly)");
            return Encoding.ASCII;
        }
    }

    private static string WriteTextReceipt(Sale sale, RuntimeSettings settings, int width)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastTPV", "Receipts");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{sale.TicketNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt");

        var receipt = new StringBuilder();

        // Company header
        foreach (var line in settings.GetReceiptHeaderLines())
            receipt.AppendLine(line);

        receipt.AppendLine(new string('-', width));
        receipt.AppendLine($"Ticket: {sale.TicketNumber}");
        receipt.AppendLine(sale.SaleDate.ToString("g"));

        foreach (var item in sale.LineItems)
            receipt.AppendLine($"{item.Quantity,3} {item.ArticleName,-24} {item.LineTotal,9:0.00}");

        receipt
            .AppendLine(new string('-', width))
            .AppendLine($"TOTAL: {sale.TotalAmount:0.00}")
            .AppendLine(sale.PaymentMethod);

        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter))
        {
            receipt.AppendLine(new string('-', width));
            receipt.AppendLine(settings.ReceiptFooter);
        }

        File.WriteAllText(path, receipt.ToString());
        return path;
    }

    private async Task PrintViaNetworkAsync(Sale sale, int width)
    {
        var settings = AppRuntime.Settings;
        var encoding = GetReceiptEncoding();

        var builder = new EscPosBuilder(encoding)
            .SelectCodePage(settings.PrinterCodePage)
            .Align(TextAlign.Center)
            .Bold(true)
            .DoubleSize(true);

        // First header line (business name) in double size
        var headerLines = settings.GetReceiptHeaderLines().ToList();
        if (headerLines.Count > 0)
        {
            builder.Line(headerLines[0]);
            builder.DoubleSize(false).Bold(false);
            for (int i = 1; i < headerLines.Count; i++)
                builder.Line(headerLines[i]);
        }
        else
        {
            builder.Line(settings.BusinessName)
                .DoubleSize(false)
                .Bold(false);
        }

        builder
            .Align(TextAlign.Left)
            .Line(new string('-', width))
            .Line($"Ticket: {sale.TicketNumber}")
            .Line(sale.SaleDate.ToString("g"));

        foreach (var item in sale.LineItems)
        {
            var name = Truncate(item.ArticleName, Math.Max(4, width - 15));
            builder.Line($"{item.Quantity,3} {name,-24} {item.LineTotal,9:0.00}");
        }

        builder
            .Line(new string('-', width))
            .Align(TextAlign.Right)
            .Bold(true)
            .Line($"TOTAL: {sale.TotalAmount:0.00}")
            .Bold(false)
            .Align(TextAlign.Center)
            .Line(sale.PaymentMethod);

        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter))
        {
            builder.Line(new string('-', width))
                .Line(settings.ReceiptFooter);
        }

        builder
            .Feed(3)
            .Cut(EscPosBuilder.ParseCutStyle(settings.PrinterCutStyle));

        // Kick the drawer at the end of a Cash sale, right after the paper cuts —
        // the natural moment for the drawer to open for making change.
        if (string.Equals(sale.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
        {
            builder.KickDrawer();
        }

        var sent = await _networkPrinter.SendAsync(
            settings.PrinterIpAddress, settings.PrinterPort, builder.ToArray(), settings.PrinterConnectTimeoutMs);

        if (!sent)
        {
            Logger.Warning(
                "Could not reach network printer at {Ip}:{Port} — receipt was saved to disk only",
                settings.PrinterIpAddress, settings.PrinterPort);
        }
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength];

    private void PrintViaWindowsShell(string path, string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName) ||
            printerName.Equals("Default Printer", StringComparison.OrdinalIgnoreCase) ||
            !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, Verb = "print", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not send receipt {Path} to printer {Printer}", path, printerName);
        }
    }
}
