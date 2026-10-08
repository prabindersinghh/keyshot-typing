namespace KeyShot.Core;

/// <summary>A raw keyboard event as observed by the input layer.</summary>
/// <param name="VirtualKey">Windows virtual-key code.</param>
/// <param name="IsKeyDown">True for key-down, false for key-up.</param>
/// <param name="IsInjected">True when the event was synthesized (SendInput etc.).</param>
/// <param name="ShortcutModifierHeld">True while Ctrl, Alt or Win is held (AltGr excluded).</param>
public readonly record struct KeyInput(
    int VirtualKey,
    bool IsKeyDown,
    bool IsInjected = false,
    bool ShortcutModifierHeld = false);
