namespace KeyShot.Core;

/// <summary>
/// Something that reacts to an accepted key press. Audio is the first effect;
/// future effects (screen shake, muzzle flash overlay, stats, ...) plug in here
/// without touching the input or filtering logic.
/// </summary>
/// <remarks>
/// <see cref="Fire"/> is called on the keyboard-hook thread. Implementations must
/// return in microseconds: hand work off to another thread, never block.
/// </remarks>
public interface IKeyEffect
{
    string Name { get; }

    void Fire(ShotKind kind);
}
