namespace KeyShot.Core;

/// <summary>Broad class of a physical key, derived from its Windows virtual-key code.</summary>
public enum KeyCategory
{
    /// <summary>Letters, digits, punctuation and numpad characters.</summary>
    Printable,
    Space,
    Enter,
    /// <summary>Ctrl, Shift, Alt, Windows and lock keys.</summary>
    Modifier,
    /// <summary>Navigation, function, editing and media keys.</summary>
    Other,
}

/// <summary>Which flavour of shot a key press should produce.</summary>
public enum ShotKind
{
    Normal,
    /// <summary>Heavier shot, used for Space.</summary>
    Heavy,
    /// <summary>Strongest shot, used for Enter.</summary>
    Strong,
}
