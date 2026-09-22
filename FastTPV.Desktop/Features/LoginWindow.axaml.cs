using Avalonia.Controls;
using Avalonia.Interactivity;
using FastTPV.Desktop.Views;

namespace FastTPV.Desktop.Features;

public partial class LoginWindow : Window
{
    public LoginWindow() => InitializeComponent();

    private async void SignIn(object? sender, RoutedEventArgs e)
    {
        Message.Text = "";

        // AppRuntime.Ready completes once the DB connection has been verified and the
        // schema/default-admin bootstrap has run. Awaiting it here avoids a race where
        // the user clicks Sign in before that startup work finishes.
        await AppRuntime.Ready;

        var userNameText = UserName.Text ?? "";
        var session = await AppRuntime.Accounts.AuthenticateAsync(userNameText, Password.Text ?? "");
        if (session is null)
        {
            // A locked-out account gets a specific message with the wait time; any
            // other failure (wrong username/password, inactive account, DB down)
            // gets the same generic message as before, so a bad guess can't be used
            // to tell which case it is.
            var lockoutRemaining = await AppRuntime.Accounts.GetLockoutRemainingAsync(userNameText);
            Message.Text = lockoutRemaining.HasValue
                ? $"Too many failed attempts. Try again in {Math.Ceiling(lockoutRemaining.Value.TotalMinutes):0} minute(s)."
                : "Invalid credentials, or the database isn't reachable — check appsettings.json.";
            return;
        }

        AppRuntime.Session.Start(session);
        await AppRuntime.Audit.WriteAsync(session, "Login", "Session");

        var main = new MainWindow();
        main.Show();
        Close();
    }
}
