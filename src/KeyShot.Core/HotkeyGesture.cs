namespace KeyShot.Core;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global hotkey such as "Ctrl+Shift+K". Modifier flags match Win32 MOD_* values.</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyModifiers.None;
        int vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": mods |= HotkeyModifiers.Ctrl; break;
                case "SHIFT": mods |= HotkeyModifiers.Shift; break;
                case "ALT": mods |= HotkeyModifiers.Alt; break;
                case "WIN" or "WINDOWS": mods |= HotkeyModifiers.Win; break;
                default:
                    if (vk != 0) return false; // two non-modifier keys
                    vk = KeyFromName(raw);
                    if (vk == 0) return false;
                    break;
            }
        }

        // Require a real key plus at least one modifier so plain typing is never hijacked.
        if (vk == 0 || mods == HotkeyModifiers.None) return false;
        gesture = new HotkeyGesture(mods, vk);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(NameFromKey(VirtualKey));
        return string.Join('+', parts);
    }

    private static int KeyFromName(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper.Length == 1 && (char.IsAsciiLetterUpper(upper[0]) || char.IsAsciiDigit(upper[0])))
            return upper[0];
        if (upper.Length >= 2 && upper[0] == 'F' && int.TryParse(upper.AsSpan(1), out int f) && f is >= 1 and <= 24)
            return 0x70 + f - 1;
        return upper switch
        {
            "SPACE" => VirtualKeys.Space,
            "ENTER" or "RETURN" => VirtualKeys.Return,
            "TAB" => VirtualKeys.Tab,
            "ESC" or "ESCAPE" => VirtualKeys.Escape,
            "BACKSPACE" => VirtualKeys.Back,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "PAUSE" => 0x13,
            _ => 0,
        };
    }

    private static string NameFromKey(int vk) => vk switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),
        VirtualKeys.Space => "Space",
        VirtualKeys.Return => "Enter",
        VirtualKeys.Tab => "Tab",
        VirtualKeys.Escape => "Esc",
        VirtualKeys.Back => "Backspace",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x13 => "Pause",
        _ => $"0x{vk:X2}",
    };
}
