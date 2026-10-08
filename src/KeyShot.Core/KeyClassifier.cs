namespace KeyShot.Core;

/// <summary>
/// Maps Windows virtual-key codes to <see cref="KeyCategory"/> values.
/// Table-driven so classification on the hook thread is a single array lookup.
/// </summary>
public static class KeyClassifier
{
    private static readonly KeyCategory[] Table = BuildTable();

    public static KeyCategory Classify(int virtualKey) =>
        (uint)virtualKey < (uint)Table.Length ? Table[virtualKey] : KeyCategory.Other;

    public static bool IsModifier(int virtualKey) => Classify(virtualKey) == KeyCategory.Modifier;

    private static KeyCategory[] BuildTable()
    {
        var t = new KeyCategory[256];
        Array.Fill(t, KeyCategory.Other);

        // Digits 0-9 and letters A-Z.
        for (int vk = 0x30; vk <= 0x39; vk++) t[vk] = KeyCategory.Printable;
        for (int vk = 0x41; vk <= 0x5A; vk++) t[vk] = KeyCategory.Printable;

        // Numpad 0-9 and * + separator - . /
        for (int vk = 0x60; vk <= 0x6F; vk++) t[vk] = KeyCategory.Printable;

        // OEM punctuation: ;=,-./` and [\]' plus the 102-key extra key.
        for (int vk = 0xBA; vk <= 0xC0; vk++) t[vk] = KeyCategory.Printable;
        for (int vk = 0xDB; vk <= 0xDF; vk++) t[vk] = KeyCategory.Printable;
        t[0xE2] = KeyCategory.Printable;

        t[VirtualKeys.Space] = KeyCategory.Space;
        t[VirtualKeys.Return] = KeyCategory.Enter;

        foreach (var vk in new[]
                 {
                     VirtualKeys.Shift, VirtualKeys.Control, VirtualKeys.Menu,
                     VirtualKeys.LShift, VirtualKeys.RShift,
                     VirtualKeys.LControl, VirtualKeys.RControl,
                     VirtualKeys.LMenu, VirtualKeys.RMenu,
                     VirtualKeys.LWin, VirtualKeys.RWin,
                     VirtualKeys.Capital, VirtualKeys.NumLock, VirtualKeys.Scroll,
                 })
        {
            t[vk] = KeyCategory.Modifier;
        }

        return t;
    }
}

/// <summary>Virtual-key codes referenced by KeyShot (subset of WinUser.h).</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int NumLock = 0x90;
    public const int Scroll = 0x91;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;
}
