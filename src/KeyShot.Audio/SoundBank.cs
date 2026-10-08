namespace KeyShot.Audio;

/// <summary>A fully decoded sound held in memory as interleaved stereo float PCM.</summary>
public sealed class SoundSample
{
    public SoundSample(string name, float[] stereoData)
    {
        if (stereoData.Length < 4 || stereoData.Length % 2 != 0)
            throw new ArgumentException("Sample must contain at least two stereo frames.", nameof(stereoData));
        Name = name;
        Data = stereoData;
    }

    public string Name { get; }

    /// <summary>Interleaved L/R samples.</summary>
    public float[] Data { get; }

    public int Frames => Data.Length / 2;
}

/// <summary>
/// The samples of one sound pack, already decoded at the output device's sample rate.
/// Heavy/Strong lists are optional; when empty the Normal samples are pitched down instead.
/// </summary>
public sealed class SoundBank
{
    public SoundBank(int sampleRate, IReadOnlyList<SoundSample> normal,
        IReadOnlyList<SoundSample>? heavy = null, IReadOnlyList<SoundSample>? strong = null)
    {
        if (normal.Count == 0) throw new ArgumentException("A sound bank needs at least one sample.", nameof(normal));
        SampleRate = sampleRate;
        Normal = normal;
        Heavy = heavy ?? [];
        Strong = strong ?? [];
    }

    public int SampleRate { get; }
    public IReadOnlyList<SoundSample> Normal { get; }
    public IReadOnlyList<SoundSample> Heavy { get; }
    public IReadOnlyList<SoundSample> Strong { get; }
}
