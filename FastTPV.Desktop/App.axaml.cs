using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FastTPV.Core.Data;
using FastTPV.Desktop.Features;
using System;
using System.IO;

namespace FastTPV.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // NOTE: this is IClassicDesktopStyleApplicationLifetime (with "Style") — an
        // earlier version of this file was missing "Style" in the type name, which
        // does not exist in Avalonia and would have failed to compile at the very
        // entry point of the app.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var settings = RuntimeSettings.Load(settingsPath);

            AppRuntime.Initialize(settings, settingsPath);

            // Login -> Main is a window handoff (Login closes itself once Main is shown).
            // The default OnMainWindowClose shutdown mode would quit the whole app the
            // moment the login window closes, so switch to OnLastWindowClose.
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = new LoginWindow();

            if (!File.Exists(settingsPath))
            {
                Serilog.Log.Warning(
                    "appsettings.json not found at {Path}; using default connection settings. " +
                    "Copy appsettings.json.template to appsettings.json and edit the connection string.",
                    settingsPath);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
