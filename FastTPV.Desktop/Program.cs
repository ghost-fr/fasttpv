using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using System;
using System.Globalization;
using System.IO;

namespace FastTPV.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "Logs", "fasttpv-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10)
            .CreateLogger();

        // FastTPV targets Spanish SMBs, so every {0:C}/{0:N} binding in the app
        // (prices, totals, tax, tendered amounts) should format as euros using
        // Spanish conventions (e.g. "1.234,50 €"), regardless of the machine's
        // Windows regional settings. Set both CurrentCulture and
        // CurrentUICulture so ReactiveUI/Avalonia bindings pick it up.
        var es = CultureInfo.GetCultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = es;
        CultureInfo.DefaultThreadCurrentUICulture = es;
        CultureInfo.CurrentCulture = es;
        CultureInfo.CurrentUICulture = es;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception on AppDomain");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
