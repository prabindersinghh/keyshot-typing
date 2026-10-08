using System.Diagnostics;

namespace KeyShot.Core;

/// <summary>
/// Drops triggers that arrive sooner than <see cref="CooldownMs"/> after the
/// previous accepted trigger. Single-threaded: only the hook thread calls it.
/// </summary>
public sealed class CooldownGate
{
    private readonly Func<long> _clock;
    private readonly long _ticksPerSecond;
    private long _lastAccepted;
    private bool _hasFired;
    private long _cooldownTicks;

    public CooldownGate(int cooldownMs = 0, Func<long>? clock = null, long ticksPerSecond = 0)
    {
        _clock = clock ?? Stopwatch.GetTimestamp;
        _ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : Stopwatch.Frequency;
        CooldownMs = cooldownMs;
    }

    public int CooldownMs
    {
        get => (int)(_cooldownTicks * 1000 / _ticksPerSecond);
        set => _cooldownTicks = Math.Max(0, value) * _ticksPerSecond / 1000;
    }

    public bool TryPass()
    {
        long now = _clock();
        if (_hasFired && now - _lastAccepted < _cooldownTicks)
            return false;

        _hasFired = true;
        _lastAccepted = now;
        return true;
    }

    public void Reset() => _hasFired = false;
}
