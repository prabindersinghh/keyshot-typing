namespace KeyShot.Audio;

/// <summary>An open audio device stream that pulls samples from a <see cref="ShotMixer"/>.</summary>
public interface IAudioOutput : IDisposable
{
    string DeviceName { get; }

    /// <summary>Native sample rate of the device; samples are decoded to match it.</summary>
    int SampleRate { get; }

    int Channels { get; }

    void Start(ShotMixer mixer);

    /// <summary>Raised when the device disappears, errors, or the default device changes.</summary>
    event Action? DeviceLost;
}

/// <summary>Creates an output bound to the current default device.</summary>
public delegate IAudioOutput AudioOutputFactory(int latencyMs);
