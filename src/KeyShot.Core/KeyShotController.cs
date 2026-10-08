namespace KeyShot.Core;

/// <summary>
/// Decides whether a raw key event should fire, and fans accepted presses out
/// to every registered <see cref="IKeyEffect"/>.
/// </summary>
/// <remarks>
/// <see cref="HandleKey"/> runs on the keyboard-hook thread and is allocation-free.
/// Settings and the enabled flag may be changed from any thread.
/// </remarks>
public sealed class KeyShotController
{
    private readonly bool[] _down = new bool[256];
    private readonly CooldownGate _cooldown;
    private volatile IKeyEffect[] _effects = [];
    private volatile KeyShotSettings _settings;
    private volatile bool _enabled;
    private long _keysSeen;
    private long _shotsFired;

    public KeyShotController(KeyShotSettings? settings = null, Func<long>? clock = null, long ticksPerSecond = 0)
    {
        _settings = (settings ?? new KeyShotSettings()).Normalize();
        _enabled = _settings.Enabled;
        _cooldown = new CooldownGate(_settings.CooldownMs, clock, ticksPerSecond);
    }

    /// <summary>Raised (on the calling thread) whenever <see cref="IsEnabled"/> changes.</summary>
    public event EventHandler<bool>? EnabledChanged;

    /// <summary>Raised when an effect throws. The exception never reaches the hook.</summary>
    public event EventHandler<Exception>? EffectFailed;

    public bool IsEnabled => _enabled;

    public KeyShotSettings Settings => _settings;

    public IReadOnlyList<IKeyEffect> Effects => _effects;

    /// <summary>Key-down events observed (diagnostics).</summary>
    public long KeysSeen => Volatile.Read(ref _keysSeen);

    /// <summary>Key presses that fired effects (diagnostics).</summary>
    public long ShotsFired => Volatile.Read(ref _shotsFired);

    public void AddEffect(IKeyEffect effect)
    {
        lock (_cooldown) _effects = [.. _effects, effect];
    }

    public void RemoveEffect(IKeyEffect effect)
    {
        lock (_cooldown) _effects = _effects.Where(e => !ReferenceEquals(e, effect)).ToArray();
    }

    public void ApplySettings(KeyShotSettings settings)
    {
        _settings = settings.Normalize();
        _cooldown.CooldownMs = _settings.CooldownMs;
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        // Key-ups may have been missed while disabled; stale "down" flags would mute the next press.
        if (enabled) Array.Clear(_down);
        _enabled = enabled;
        EnabledChanged?.Invoke(this, enabled);
    }

    public void Toggle() => SetEnabled(!_enabled);

    /// <summary>
    /// Pure filter: returns the shot to play for this event, or null to stay silent.
    /// Updates key-down tracking for auto-repeat detection.
    /// </summary>
    public ShotKind? Evaluate(in KeyInput input)
    {
        int vk = input.VirtualKey & 0xFF;
        var s = _settings;

        if (!input.IsKeyDown)
        {
            _down[vk] = false;
            return null;
        }

        bool repeat = _down[vk];
        _down[vk] = true;

        if (!_enabled) return null;
        if (input.IsInjected && s.IgnoreInjected) return null;
        if (repeat && s.IgnoreAutoRepeat) return null;

        var category = KeyClassifier.Classify(vk);
        if (category == KeyCategory.Modifier)
            return s.FireOnModifiers ? ShotKind.Normal : null;

        if (input.ShortcutModifierHeld && s.IgnoreShortcuts) return null;

        return category switch
        {
            KeyCategory.Printable => ShotKind.Normal,
            KeyCategory.Space => s.HeavySpace ? ShotKind.Heavy : ShotKind.Normal,
            KeyCategory.Enter => s.StrongEnter ? ShotKind.Strong : ShotKind.Normal,
            _ => s.FireOnOtherKeys ? ShotKind.Normal : null,
        };
    }

    /// <summary>Hook entry point. Returns true when effects were fired. Never throws.</summary>
    public bool HandleKey(in KeyInput input)
    {
        if (input.IsKeyDown) Volatile.Write(ref _keysSeen, _keysSeen + 1); // single writer: hook thread

        var shot = Evaluate(input);
        if (shot is null || !_cooldown.TryPass()) return false;

        Volatile.Write(ref _shotsFired, _shotsFired + 1);
        foreach (var effect in _effects)
        {
            try
            {
                effect.Fire(shot.Value);
            }
            catch (Exception ex)
            {
                try { EffectFailed?.Invoke(this, ex); } catch { /* never propagate */ }
            }
        }
        return true;
    }
}
