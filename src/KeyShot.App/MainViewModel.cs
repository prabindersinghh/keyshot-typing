using System.ComponentModel;

namespace KeyShot.App;

/// <summary>Thin binding layer over <see cref="AppHost"/>; holds no state of its own.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppHost _host;
    private bool _capturingHotkey;

    public MainViewModel(AppHost host)
    {
        _host = host;
        _host.Changed += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsActive => _host.IsEnabled;
    public string StatusText => IsActive ? "ACTIVE" : "INACTIVE";
    public string ToggleText => IsActive ? "DISABLE" : "ENABLE";

    public int Volume
    {
        get => _host.Settings.Volume;
        set => _host.Update(s => s with { Volume = value });
    }

    public int Variation
    {
        get => _host.Settings.Variation;
        set => _host.Update(s => s with { Variation = value });
    }

    public int Cooldown
    {
        get => _host.Settings.CooldownMs;
        set => _host.Update(s => s with { CooldownMs = value });
    }

    public IReadOnlyList<string> Modes => _host.AvailableModes;

    public string Mode
    {
        get => _host.Settings.Mode;
        set { if (value != null) _host.Update(s => s with { Mode = value }); }
    }

    public bool HeavySpace
    {
        get => _host.Settings.HeavySpace;
        set => _host.Update(s => s with { HeavySpace = value });
    }

    public bool StrongEnter
    {
        get => _host.Settings.StrongEnter;
        set => _host.Update(s => s with { StrongEnter = value });
    }

    public bool CapturingHotkey
    {
        get => _capturingHotkey;
        set
        {
            _capturingHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public string HotkeyText => CapturingHotkey ? "Press a key combination…" : _host.Settings.Hotkey;
    public string HotkeyButtonText => CapturingHotkey ? "Cancel" : "Change";
    public string DeviceText => _host.AudioDeviceText;
    public string AudioWarning => _host.Warning;
    public string VersionText => "v" + AppHost.Version;
}
