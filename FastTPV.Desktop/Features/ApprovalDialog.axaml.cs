using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Verifies an Admin's credentials before allowing an action a Cashier isn't
/// otherwise permitted to do on their own (currently: discounts above
/// POS.MaxCashierDiscountPercent). Show with:
///   var approved = await new ApprovalDialog("...reason...").ShowDialog&lt;bool&gt;(ownerWindow);
/// </summary>
public partial class ApprovalDialog : Window
{
    public ApprovalDialog() : this("This action needs an Admin to approve it.")
    {
    }

    public ApprovalDialog(string reason)
    {
        InitializeComponent();
        Reason.Text = reason;
    }

    private async void Approve(object? sender, RoutedEventArgs e)
    {
        Message.Text = "";

        var session = await AppRuntime.Accounts.AuthenticateAsync(Username.Text ?? "", Password.Text ?? "");
        if (session is null || session.Role != Roles.Admin)
        {
            Message.Text = "Invalid admin credentials.";
            return;
        }

        Close(true);
    }

    private void Cancel(object? sender, RoutedEventArgs e) => Close(false);
}
