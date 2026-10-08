using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using KeyShot.Audio;
using KeyShot.Core;
using KeyShot.Input;

namespace KeyShot.App;

/// <summary>
/// Composition root: wires input -> controller -> effects, and owns the
/// tray, hotkey, control pipe and window. Everything here runs on the UI thread
/// except the hook callback (hook thread) and the mixer (audio thread).
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsStore _store = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _watchdog;
    private KeyShotController _controller = null!;
    private AudioEngine _audio = null!;
    private LowLevelKeyboardHook _hook = null!;
    private GlobalHotkey _hotkey = null!;
    private TrayIcon _tray = null!;
    private ControlPipeServer _pipe = null!;
    private MainWindow? _window;
    private string _hotkeyWarning = "";
    private string _inputWarning = "";
    private bool _trayHintShown;
    private bool _disposed;

    public AppHost(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) =>
        {
            _saveTimer!.Stop();
            _store.Save(Settings);
        }, dispatcher) { IsEnabled = false };
        _watchdog = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => Watchdog(), dispatcher);
    }

    /// <summary>Raised on the UI thread whenever anything the UI shows may have changed.</summary>
    public event Action? Changed;

    public static string SoundsRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Sounds");

    public static string Version { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";

    public KeyShotSettings Settings => _controller.Settings;
    public bool IsEnabled => _controller.IsEnabled;
    public IReadOnlyList<string> AvailableModes { get; private set; } = [];

    public string AudioDeviceText => _audio.State switch
    {
        AudioEngineState.Running => _audio.DeviceName,
        AudioEngineState.Faulted => "Unavailable",
        _ => _audio.DeviceName == "—" ? "Idle" : $"{_audio.DeviceName} (idle)",
    };

    public string Warning =>
        _audio.State == AudioEngineState.Faulted
            ? $"Audio unavailable ({_audio.LastError?.Message}). Retrying…"
            : _inputWarning.Length > 0 ? _inputWarning : _hotkeyWarning;

    public void Start(bool showWindow)
    {
        AvailableModes = SoundLoader.AvailableModes(SoundsRoot);
        var settings = _store.Load();
        if (AvailableModes.Count > 0 && !AvailableModes.Contains(settings.Mode, StringComparer.OrdinalIgnoreCase))
            settings = settings with { Mode = AvailableModes[0] };

        _controller = new KeyShotController(settings);
        _controller.EffectFailed += (_, ex) => Log.Error("Effect failed", ex);

        _audio = new AudioEngine(SoundsRoot);
        _audio.Configure(settings.Volume, settings.Variation, settings.Mode, settings.AudioLatencyMs);
        _audio.StateChanged += (_, _) => _dispatcher.BeginInvoke(RaiseChanged);
        _controller.AddEffect(_audio);

        _hook = new LowLevelKeyboardHook(input => _controller.HandleKey(input));
        _hook.HandlerFailed += ex => Log.Error("Hook handler", ex);

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += Toggle;
        RegisterHotkey(settings.Hotkey);

        _tray = new TrayIcon(() => SetEnabled(true), () => SetEnabled(false), ShowWindow, Exit);

        _pipe = new ControlPipeServer(HandleCommand, _dispatcher);
        _pipe.Start();

        _controller.EnabledChanged += (_, on) => ApplyEnabled(on);
        ApplyEnabled(_controller.IsEnabled);
        _watchdog.Start();

        _window = new MainWindow(this);
        if (showWindow) ShowWindow();
        Log.Info($"KeyShot {Version} started (enabled={IsEnabled}, mode={settings.Mode}, device={_audio.DeviceName})");
    }

    public void SetEnabled(bool enabled) => _controller.SetEnabled(enabled);

    public void Toggle() => _controller.Toggle();

    /// <summary>Plays a shot through the engine regardless of the enabled state (UI "Test" button).</summary>
    public void TestShot()
    {
        if (_audio.State != AudioEngineState.Running) _audio.Start();
        _audio.Fire(ShotKind.Normal);
        if (!IsEnabled) ScheduleIdleStop();
    }

    public void Update(Func<KeyShotSettings, KeyShotSettings> change)
    {
        var next = change(Settings).Normalize();
        if (next == Settings) return;
        _controller.ApplySettings(next);
        _audio.Configure(next.Volume, next.Variation, next.Mode, next.AudioLatencyMs);
        _saveTimer.Stop();
        _saveTimer.Start();
        RaiseChanged();
    }

    public void BeginHotkeyCapture() => _hotkey.Unregister();

    /// <summary>Applies a captured hotkey, or restores the previous one when null/unavailable.</summary>
    public bool EndHotkeyCapture(HotkeyGesture? gesture)
    {
        if (gesture is { } g && _hotkey.Register(g))
        {
            _hotkeyWarning = "";
            Update(s => s with { Hotkey = g.ToString() });
            _tray.SetState(IsEnabled, Settings.Hotkey);
            return true;
        }

        bool ok = RegisterHotkey(Settings.Hotkey);
        if (gesture is { } failed && ok)
            _hotkeyWarning = $"{failed} is already used by another app.";
        RaiseChanged();
        return false;
    }

    public void ShowWindow()
    {
        if (_window == null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true; // reliably bring to front when invoked from tray/pipe
        _window.Topmost = false;
        _window.Focus();
    }

    /// <summary>Called when the user closes the window.</summary>
    public void OnWindowClosedToTray()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        _tray.Notify("KeyShot is still running", $"It lives in the tray. Toggle anytime with {Settings.Hotkey}.");
    }

    public void Exit()
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watchdog.Stop();
        _saveTimer.Stop();
        _store.Save(Settings);
        _pipe.Dispose();
        _hook.Dispose();
        _audio.Dispose();
        _hotkey.Dispose();
        _tray.Dispose();
        Log.Info("KeyShot exited");
    }

    internal string HandleCommand(string command)
    {
        switch (command)
        {
            case "ENABLE": SetEnabled(true); break;
            case "DISABLE": SetEnabled(false); break;
            case "TOGGLE": Toggle(); break;
            case "SHOW": ShowWindow(); break;
            case "PING": return "PONG";
            case "STATS":
                return $"{(IsEnabled ? "ACTIVE" : "INACTIVE")} keys={_controller.KeysSeen} shots={_controller.ShotsFired} " +
                       $"voices={_audio.ActiveVoices} audio={_audio.State} hook={_hook.IsRunning} " +
                       $"hotkey={_hotkey.Current?.ToString() ?? "unregistered"} device=\"{_audio.DeviceName}\"";
            case "STATUS": break;
            default: return "ERR unknown command";
        }
        return IsEnabled ? "ACTIVE" : "INACTIVE";
    }

    private void ApplyEnabled(bool on)
    {
        if (on)
        {
            // Audio first, so the very first keystroke after enabling already has a live stream.
            if (!_audio.Start()) Log.Error("Audio start failed", _audio.LastError);
            try
            {
                _hook.Start();
                _inputWarning = "";
            }
            catch (Exception ex)
            {
                _inputWarning = "Could not install the keyboard hook.";
                Log.Error("Hook start failed", ex);
            }
        }
        else
        {
            // Fully idle while disabled: no hook in the input chain, no audio stream open.
            _hook.Stop();
            _audio.Stop();
        }

        _tray.SetState(on, Settings.Hotkey);
        if (Settings.Enabled != on) Update(s => s with { Enabled = on });
        RaiseChanged();
    }

    private bool RegisterHotkey(string text)
    {
        if (!HotkeyGesture.TryParse(text, out var gesture)) return false;
        bool ok = _hotkey.Register(gesture);
        _hotkeyWarning = ok ? "" : $"Hotkey {gesture} is used by another app. Pick another.";
        if (!ok) Log.Error($"RegisterHotKey failed for {gesture}");
        return ok;
    }

    private void Watchdog()
    {
        // Recover from unplugged/changed devices that could not be reopened immediately.
        if (IsEnabled && _audio.State == AudioEngineState.Faulted) _audio.Start();
    }

    private DispatcherTimer? _idleStop;

    private void ScheduleIdleStop()
    {
        _idleStop ??= new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.Background, (_, _) =>
        {
            _idleStop!.Stop();
            if (!IsEnabled) _audio.Stop();
        }, _dispatcher);
        _idleStop.Stop();
        _idleStop.Start();
    }

    private void RaiseChanged()
    {
        if (!_disposed) Changed?.Invoke();
    }
}
