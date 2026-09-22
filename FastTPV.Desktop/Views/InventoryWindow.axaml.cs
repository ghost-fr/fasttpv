using Avalonia.Controls;
using FastTPV.Desktop.ViewModels;

namespace FastTPV.Desktop.Views;

public partial class InventoryWindow : Window
{
    public InventoryWindow()
    {
        InitializeComponent();
    }

    public InventoryWindow(InventoryWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
