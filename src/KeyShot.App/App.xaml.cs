using System.Threading;
using System.Windows;

namespace KeyShot.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Command-line control: KeyShot.exe [--enable | --disable | --toggle] [--minimized]
        string? command = null;
        foreach (var arg in e.Args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "--enable": command = "ENABLE"; break;
                case "--disable": command = "DISABLE"; break;
                case "--toggle": command = "TOGGLE"; break;
            }
        }
        bool minimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        _singleInstance = new Mutex(true, @"Local\KeyShot.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            // Already running: forward the request (or just bring its window up) and leave.
            ControlPipeClient.TrySend(command ?? "SHOW");
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true; // a UI glitch must never take down the keyboard hook
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);

        try
        {
            _host = new AppHost(Dispatcher);
            _host.Start(showWindow: !minimized);
            if (command != null) _host.HandleCommand(command);
        }
        catch (Exception ex)
        {
            // Never linger as an invisible, half-started process.
            Log.Error("Startup failed", ex);
            MessageBox.Show($"KeyShot could not start:\n\n{ex.Message}", "KeyShot", MessageBoxButton.OK, MessageBoxImage.Error);
            _host = null;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        if (_singleInstance != null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}
