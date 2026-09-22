using Avalonia.Controls;
using Avalonia.Input;
using FastTPV.Desktop.ViewModels;

namespace FastTPV.Desktop.Views;

public partial class SalesWindow : Window
{
    public SalesWindow()
    {
        InitializeComponent();
    }

    public SalesWindow(SalesWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.OwnerWindow = this;
    }

    // Barcode scanners behave like a keyboard typing very fast, then sending Enter.
    // As long as the search box has focus (typical — scanners are usually configured
    // to just type into whatever's focused), Enter here tries an exact code match
    // and adds it straight to the cart instead of just filtering the product list.
    private void SearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SalesWindowViewModel vm) vm.TryQuickAddByCode();
    }
}
