using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FastTPV.Desktop.Features;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow() => InitializeComponent();

    private async void Submit(object? sender, RoutedEventArgs e)
    {
        Message.Text = "";

        var user = AppRuntime.Session.CurrentUser;
        if (user is null)
        {
            Message.Text = "Your session has expired — please sign in again.";
            return;
        }

        if (NewPassword.Text != ConfirmPassword.Text)
        {
            Message.Text = "New password and confirmation don't match.";
            return;
        }

        if (!UserAccountService.IsValidPassword(NewPassword.Text ?? "", AppRuntime.Settings.MinPasswordLength))
        {
            Message.Text = $"Password must be at least {AppRuntime.Settings.MinPasswordLength} characters " +
                            "and include an uppercase letter, a lowercase letter, and a digit.";
            return;
        }

        var ok = await AppRuntime.Accounts.ChangeOwnPasswordAsync(user.UserId, CurrentPassword.Text ?? "", NewPassword.Text!);
        if (!ok)
        {
            Message.Text = "Current password is incorrect.";
            return;
        }

        await AppRuntime.Audit.WriteAsync(user, "ChangePassword", "User", user.UserName);
        Close();
    }
}
