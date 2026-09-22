using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FastTPV.Core.Models;

namespace FastTPV.Desktop.Features;

public partial class SupplierWindow : Window
{
    private readonly ObservableCollection<Supplier> _suppliers = new();
    private Supplier? _selected;

    public SupplierWindow()
    {
        InitializeComponent();
        SupplierList.ItemsSource = _suppliers;
        Opened += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _suppliers.Clear();
        foreach (var supplier in await AppRuntime.Suppliers.GetAllAsync())
            _suppliers.Add(supplier);
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        var text = Search.Text?.Trim() ?? "";
        SupplierList.ItemsSource = string.IsNullOrEmpty(text)
            ? _suppliers
            : _suppliers.Where(s =>
                s.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                s.Code.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                s.Email.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void SupplierSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SupplierList.SelectedItem is not Supplier supplier) return;

        _selected = supplier;
        Code.Text = supplier.Code;
        Name.Text = supplier.Name;
        ContactName.Text = supplier.ContactName;
        Email.Text = supplier.Email;
        Phone.Text = supplier.Phone;
        TaxId.Text = supplier.TaxId;
        Address.Text = supplier.Address;
        IsActive.IsChecked = supplier.IsActive;
        Message.Text = "";
    }

    private void NewSupplier(object? sender, RoutedEventArgs e)
    {
        _selected = null;
        SupplierList.SelectedItem = null;
        Code.Text = Name.Text = ContactName.Text = Email.Text = Phone.Text = TaxId.Text = Address.Text = "";
        IsActive.IsChecked = true;
        Message.Text = "";
    }

    private async void Save(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Code.Text) || string.IsNullOrWhiteSpace(Name.Text))
        {
            Message.Text = "Code and Name are required.";
            return;
        }

        var supplier = _selected ?? new Supplier();
        supplier.Code = Code.Text.Trim();
        supplier.Name = Name.Text.Trim();
        supplier.ContactName = ContactName.Text ?? "";
        supplier.Email = Email.Text ?? "";
        supplier.Phone = Phone.Text ?? "";
        supplier.TaxId = TaxId.Text ?? "";
        supplier.Address = Address.Text ?? "";
        supplier.IsActive = IsActive.IsChecked == true;

        var ok = supplier.Id == 0
            ? await AppRuntime.Suppliers.AddAsync(supplier) > 0
            : await AppRuntime.Suppliers.UpdateAsync(supplier);

        Message.Text = ok ? "Supplier saved." : "Unable to save supplier — is the code already used?";
        await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, supplier.Id == 0 ? "Create" : "Update", "Supplier", supplier.Code);

        if (ok) await LoadAsync();
    }

    private async void Delete(object? sender, RoutedEventArgs e)
    {
        if (_selected?.Id is not > 0) return;

        if (await AppRuntime.Suppliers.DeleteAsync(_selected.Id))
        {
            await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, "Delete", "Supplier", _selected.Code);
            Message.Text = "Supplier deleted.";
            await LoadAsync();
        }
    }
}
