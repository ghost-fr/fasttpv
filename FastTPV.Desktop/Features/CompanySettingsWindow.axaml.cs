using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Admin-only screen for editing company / business identity that appears on
/// receipts (name, tax ID, address, contact, header/footer). Changes are written
/// both to the in-memory RuntimeSettings (so the next receipt uses them immediately)
/// and to appsettings.json via AppRuntime.PersistCompanySettings().
/// </summary>
public partial class CompanySettingsWindow : Window
{
    public CompanySettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var s = AppRuntime.Settings;

        BusinessName.Text = s.BusinessName;
        TradeName.Text = s.TradeName;
        TaxId.Text = s.TaxId;
        AddressLine1.Text = s.AddressLine1;
        AddressLine2.Text = s.AddressLine2;
        City.Text = s.City;
        PostalCode.Text = s.PostalCode;
        Province.Text = s.Province;
        Country.Text = s.Country;
        Phone.Text = s.Phone;
        Email.Text = s.Email;
        Website.Text = s.Website;
        ReceiptHeader.Text = s.ReceiptHeader;
        ReceiptFooter.Text = s.ReceiptFooter;
        LogoPath.Text = s.LogoPath;

        Message.Text = "";
    }

    private void Cancel(object? sender, RoutedEventArgs e) => Close();

    private async void Save(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BusinessName.Text))
        {
            Message.Foreground = Avalonia.Media.Brush.Parse("#B91C1C");
            Message.Text = "Business name is required.";
            return;
        }

        var s = AppRuntime.Settings;
        s.BusinessName = BusinessName.Text.Trim();
        s.TradeName = TradeName.Text?.Trim() ?? "";
        s.TaxId = TaxId.Text?.Trim() ?? "";
        s.AddressLine1 = AddressLine1.Text?.Trim() ?? "";
        s.AddressLine2 = AddressLine2.Text?.Trim() ?? "";
        s.City = City.Text?.Trim() ?? "";
        s.PostalCode = PostalCode.Text?.Trim() ?? "";
        s.Province = Province.Text?.Trim() ?? "";
        s.Country = Country.Text?.Trim() ?? "";
        s.Phone = Phone.Text?.Trim() ?? "";
        s.Email = Email.Text?.Trim() ?? "";
        s.Website = Website.Text?.Trim() ?? "";
        s.ReceiptHeader = ReceiptHeader.Text?.Trim() ?? "";
        s.ReceiptFooter = string.IsNullOrWhiteSpace(ReceiptFooter.Text)
            ? "Thank you for your purchase!"
            : ReceiptFooter.Text.Trim();
        s.LogoPath = LogoPath.Text?.Trim() ?? "";

        try
        {
            AppRuntime.PersistCompanySettings();
        }
        catch (Exception ex)
        {
            Message.Foreground = Avalonia.Media.Brush.Parse("#B91C1C");
            Message.Text = $"Settings applied for this session, but could not be saved to disk: {ex.Message}";
            return;
        }

        try
        {
            await AppRuntime.Audit.WriteAsync(
                AppRuntime.Session.CurrentUser, "Update", "CompanySettings", s.BusinessName);
        }
        catch
        {
            // Audit failure should not block a successful save
        }

        Message.Foreground = Avalonia.Media.Brush.Parse("#166534");
        Message.Text = "Company settings saved. New receipts will use these details immediately — no restart needed.";
    }
}
