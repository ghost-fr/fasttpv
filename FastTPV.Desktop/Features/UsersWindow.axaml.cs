using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FastTPV.Desktop.Features;

public partial class UsersWindow : Window
{
    private List<UserAccount> _users = new();
    private int _selectedId;

    public UsersWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _users = await AppRuntime.Accounts.GetAllAsync();
        UserList.ItemsSource = _users;
    }

    private void NewUser(object? sender, RoutedEventArgs e)
    {
        _selectedId = 0;
        UserList.SelectedItem = null;
        UserName.Text = "";
        UserName.IsReadOnly = false;
        DisplayName.Text = "";
        Role.SelectedIndex = 0; // Cashier
        IsActive.IsChecked = true;
        NewPassword.Text = "";
        Message.Text = "";
    }

    private void UserSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (UserList.SelectedItem is not UserAccount user) return;

        _selectedId = user.Id;
        UserName.Text = user.UserName;
        UserName.IsReadOnly = true; // usernames don't change once created
        DisplayName.Text = user.DisplayName;
        Role.SelectedIndex = user.Role == Roles.Admin ? 1 : 0;
        IsActive.IsChecked = user.IsActive;
        NewPassword.Text = "";
        Message.Text = "";
    }

    private async void Save(object? sender, RoutedEventArgs e)
    {
        var role = (Role.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? Roles.Cashier;
        var isActive = IsActive.IsChecked == true;
        var isSelf = AppRuntime.Session.CurrentUser?.UserId == _selectedId && _selectedId != 0;

        if (isSelf && (!isActive || role != Roles.Admin))
        {
            Message.Text = "You can't deactivate your own account or remove your own Admin role — have another admin do it.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName.Text))
        {
            Message.Text = "Display name is required.";
            return;
        }

        if (_selectedId == 0)
        {
            if (string.IsNullOrWhiteSpace(UserName.Text))
            {
                Message.Text = "Username is required.";
                return;
            }

            if (!UserAccountService.IsValidPassword(NewPassword.Text ?? "", AppRuntime.Settings.MinPasswordLength))
            {
                Message.Text = $"Password must be at least {AppRuntime.Settings.MinPasswordLength} characters " +
                                "and include an uppercase letter, a lowercase letter, and a digit.";
                return;
            }

            var added = await AppRuntime.Accounts.AddAsync(UserName.Text.Trim(), DisplayName.Text.Trim(), NewPassword.Text!, role);
            if (!added)
            {
                Message.Text = "Could not create the user — is the username already taken?";
                return;
            }

            await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, "Create", "User", UserName.Text.Trim());
            Message.Text = "User created.";
        }
        else
        {
            await AppRuntime.Accounts.SetRoleAsync(_selectedId, role);
            await AppRuntime.Accounts.SetActiveAsync(_selectedId, isActive);
            await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, "Update", "User", UserName.Text);

            if (!string.IsNullOrWhiteSpace(NewPassword.Text))
            {
                if (!UserAccountService.IsValidPassword(NewPassword.Text, AppRuntime.Settings.MinPasswordLength))
                {
                    Message.Text = $"User saved, but the password was NOT reset — it must be at least " +
                                    $"{AppRuntime.Settings.MinPasswordLength} characters with an uppercase letter, " +
                                    "a lowercase letter, and a digit.";
                    await LoadAsync();
                    return;
                }

                await AppRuntime.Accounts.SetPasswordAsync(_selectedId, NewPassword.Text);
                await AppRuntime.Audit.WriteAsync(AppRuntime.Session.CurrentUser, "ResetPassword", "User", UserName.Text);
                Message.Text = "User saved and password reset.";
            }
            else
            {
                Message.Text = "User saved.";
            }
        }

        await LoadAsync();
    }
}
