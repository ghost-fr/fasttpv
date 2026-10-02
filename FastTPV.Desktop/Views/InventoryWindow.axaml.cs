using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FastTPV.Desktop.ViewModels;

namespace FastTPV.Desktop.Views;

public partial class InventoryWindow : Window
{
    private static readonly FilePickerFileType XlsxType = new("Excel workbook")
    {
        Patterns = new[] { "*.xlsx" },
        MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
    };

    public InventoryWindow()
    {
        InitializeComponent();
    }

    public InventoryWindow(InventoryWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>Asks where to save, then hands the path to the ViewModel (which does the work and reports status).</summary>
    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InventoryWindowViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export products to Excel",
            SuggestedFileName = $"FastTPV-products-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[] { XlsxType }
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return; // cancelled

        await vm.ExportToAsync(path);
    }

    /// <summary>Asks which file to import, then hands the stream to the ViewModel.</summary>
    private async void ImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InventoryWindowViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import products from Excel",
            AllowMultiple = false,
            FileTypeFilter = new[] { XlsxType }
        });
        if (files.Count == 0) return; // cancelled

        await using var stream = await files[0].OpenReadAsync();
        // Copy to memory: ClosedXML needs a seekable stream and the picker's stream may not be.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        buffer.Position = 0;
        await vm.ImportFromAsync(buffer, files[0].Name);
    }
}
