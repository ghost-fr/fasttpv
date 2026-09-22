using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Admin-only screen (see MainWindow's IsAdmin-gated "Printer Settings" nav item)
/// for configuring and verifying a thermal printer without editing appsettings.json
/// by hand. Designed around the fact that there is no reliable brand→settings table
/// for ESC/POS printers — firmware compatibility varies too much even within one
/// manufacturer's lineup — so instead this lets the installer pick a starting point
/// and use "Send test print" to confirm it against the physical printer in front of
/// them before saving.
/// </summary>
public partial class PrinterSettingsWindow : Window
{
    public PrinterSettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var s = AppRuntime.Settings;

        Connection.SelectedIndex = s.PrinterConnection switch
        {
            "Network" => 1,
            "WindowsShell" => 2,
            _ => 0
        };
        IpAddress.Text = s.PrinterIpAddress;
        Port.Text = s.PrinterPort.ToString();
        PrinterName.Text = s.PrinterName;

        SelectComboByTag(PaperWidth, s.ReceiptFormat == "58mm" ? "58mm" : "80mm");
        SelectComboByTag(CodePage, s.PrinterCodePage.ToString());
        SelectComboByTag(CutStyle, s.PrinterCutStyle);

        Message.Text = "";
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
        if (combo.SelectedItem is null && combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private string SelectedConnection() => (Connection.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
    private string SelectedCutStyle() => (CutStyle.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Standard";
    private string SelectedPaperWidth() => (PaperWidth.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "80mm";

    private int SelectedCodePage()
        => int.TryParse((CodePage.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var v) ? v : 16;

    private async void TestPrint(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection() != "Network")
        {
            Message.Text = "Test print only works with connection type \"Network\" — select that first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(IpAddress.Text))
        {
            Message.Text = "Enter the printer's IP address first.";
            return;
        }

        if (!int.TryParse(Port.Text, out var port))
        {
            Message.Text = "Port must be a number.";
            return;
        }

        TestPrintButton.IsEnabled = false;
        Message.Text = "Sending test page...";

        try
        {
            var (sent, message) = await AppRuntime.Receipts.SendTestPrintAsync(
                IpAddress.Text.Trim(), port, SelectedCodePage(), SelectedCutStyle(),
                AppRuntime.Settings.PrinterConnectTimeoutMs, AppRuntime.Settings.BusinessName, SelectedPaperWidth());

            Message.Text = message;
            Message.Foreground = sent
                ? Avalonia.Media.Brush.Parse("#166534")
                : Avalonia.Media.Brush.Parse("#B91C1C");
        }
        finally
        {
            TestPrintButton.IsEnabled = true;
        }
    }

    private async void Save(object? sender, RoutedEventArgs e)
    {
        var connection = SelectedConnection();

        if (connection == "Network")
        {
            if (string.IsNullOrWhiteSpace(IpAddress.Text))
            {
                Message.Text = "Enter the printer's IP address before saving.";
                return;
            }
            if (!int.TryParse(Port.Text, out _))
            {
                Message.Text = "Port must be a number.";
                return;
            }
        }

        AppRuntime.Settings.PrinterConnection = connection;
        AppRuntime.Settings.PrinterIpAddress = IpAddress.Text?.Trim() ?? "";
        AppRuntime.Settings.PrinterPort = int.TryParse(Port.Text, out var p) ? p : AppRuntime.Settings.PrinterPort;
        AppRuntime.Settings.PrinterName = string.IsNullOrWhiteSpace(PrinterName.Text)
            ? AppRuntime.Settings.PrinterName : PrinterName.Text.Trim();
        AppRuntime.Settings.ReceiptFormat = SelectedPaperWidth();
        AppRuntime.Settings.PrinterCodePage = SelectedCodePage();
        AppRuntime.Settings.PrinterCutStyle = SelectedCutStyle();

        try
        {
            AppRuntime.PersistPrinterSettings();
        }
        catch (Exception ex)
        {
            Message.Text = $"Settings applied for this session, but could not be saved to disk: {ex.Message}";
            return;
        }

        await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, "Update", "PrinterSettings", connection);

        Message.Foreground = Avalonia.Media.Brush.Parse("#166534");
        Message.Text = "Printer settings saved. New sales will use these settings immediately — no restart needed.";
    }
}
