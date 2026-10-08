namespace KeyShot.Core;

/// <summary>User configuration, persisted as JSON.</summary>
public sealed record KeyShotSettings
{
    /// <summary>Whether sounds are active. Restored on next launch.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Master volume, 0-100.</summary>
    public int Volume { get; init; } = 80;

    /// <summary>Random pitch/volume variation, 0-100.</summary>
    public int Variation { get; init; } = 35;

    /// <summary>Minimum milliseconds between two shots, 0-250.</summary>
    public int CooldownMs { get; init; } = 15;

    /// <summary>Sound pack (folder name under Sounds/).</summary>
    public string Mode { get; init; } = "Shotgun";

    /// <summary>Space plays a heavier, deeper shot.</summary>
    public bool HeavySpace { get; init; } = true;

    /// <summary>Enter plays the strongest shot.</summary>
    public bool StrongEnter { get; init; } = true;

    /// <summary>Also fire on Backspace, Tab, arrows, F-keys etc.</summary>
    public bool FireOnOtherKeys { get; init; } = false;

    /// <summary>Also fire on modifier keys (Ctrl, Shift, Alt, Win).</summary>
    public bool FireOnModifiers { get; init; } = false;

    /// <summary>Ignore keys pressed while Ctrl/Alt/Win is held (shortcuts like Ctrl+C).</summary>
    public bool IgnoreShortcuts { get; init; } = true;

    /// <summary>Ignore synthetic keyboard events produced by other software.</summary>
    public bool IgnoreInjected { get; init; } = true;

    /// <summary>Fire only once per physical press, not on keyboard auto-repeat.</summary>
    public bool IgnoreAutoRepeat { get; init; } = true;

    /// <summary>Global toggle hotkey, e.g. "Ctrl+Shift+K".</summary>
    public string Hotkey { get; init; } = "Ctrl+Shift+K";

    /// <summary>Requested WASAPI buffer in milliseconds (lower = less latency).</summary>
    public int AudioLatencyMs { get; init; } = 10;

    /// <summary>Closing the window hides it to the tray instead of exiting.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>Returns a copy with every value clamped into its valid range.</summary>
    public KeyShotSettings Normalize() => this with
    {
        Volume = Math.Clamp(Volume, 0, 100),
        Variation = Math.Clamp(Variation, 0, 100),
        CooldownMs = Math.Clamp(CooldownMs, 0, 250),
        AudioLatencyMs = Math.Clamp(AudioLatencyMs, 3, 100),
        Mode = string.IsNullOrWhiteSpace(Mode) ? "Shotgun" : Mode.Trim(),
        Hotkey = HotkeyGesture.TryParse(Hotkey, out _) ? Hotkey : "Ctrl+Shift+K",
    };
}
