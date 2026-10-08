using KeyShot.Core;

namespace KeyShot.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "keyshot-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_are_sensible()
    {
        var s = new KeyShotSettings();
        Assert.True(s.Enabled);
        Assert.Equal("Ctrl+Shift+K", s.Hotkey);
        Assert.False(s.FireOnModifiers);
        Assert.True(s.IgnoreInjected);
        Assert.Equal("Pump Shotgun", s.Mode);
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var store = new SettingsStore(FilePath);
        Assert.Equal(new KeyShotSettings(), store.Load());
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var store = new SettingsStore(FilePath);
        var settings = new KeyShotSettings { Volume = 42, Variation = 7, CooldownMs = 33, Enabled = false, Hotkey = "Ctrl+Alt+F9" };

        Assert.True(store.Save(settings));
        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not json");
        Assert.Equal(new KeyShotSettings(), new SettingsStore(FilePath).Load());
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_missing_values()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "volume": 10, /* comment */ }""");
        var loaded = new SettingsStore(FilePath).Load();
        Assert.Equal(10, loaded.Volume);
        Assert.Equal(new KeyShotSettings().Variation, loaded.Variation);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var s = new KeyShotSettings { Volume = 500, Variation = -3, CooldownMs = 9999, AudioLatencyMs = 0, Mode = "  ", Hotkey = "nonsense" }.Normalize();
        Assert.Equal(100, s.Volume);
        Assert.Equal(0, s.Variation);
        Assert.Equal(250, s.CooldownMs);
        Assert.Equal(3, s.AudioLatencyMs);
        Assert.Equal("Pump Shotgun", s.Mode);
        Assert.Equal("Ctrl+Shift+K", s.Hotkey);
    }

    [Theory]
    [InlineData("Ctrl+Shift+K", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 'K')]
    [InlineData("alt + f12", HotkeyModifiers.Alt, 0x7B)]
    [InlineData("Win+Space", HotkeyModifiers.Win, VirtualKeys.Space)]
    [InlineData("Control+Alt+1", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, '1')]
    public void Hotkeys_parse(string text, HotkeyModifiers mods, int vk)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.Equal(mods, g.Modifiers);
        Assert.Equal(vk, g.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("K")]            // no modifier: would hijack typing
    [InlineData("Ctrl+Shift")]   // no key
    [InlineData("Ctrl+K+J")]     // two keys
    [InlineData("Ctrl+Banana")]
    public void Invalid_hotkeys_are_rejected(string text) =>
        Assert.False(HotkeyGesture.TryParse(text, out _));

    [Fact]
    public void Hotkey_round_trips_to_text()
    {
        Assert.True(HotkeyGesture.TryParse("shift+ctrl+k", out var g));
        Assert.Equal("Ctrl+Shift+K", g.ToString());
        Assert.True(HotkeyGesture.TryParse(g.ToString(), out var again));
        Assert.Equal(g, again);
    }
}
