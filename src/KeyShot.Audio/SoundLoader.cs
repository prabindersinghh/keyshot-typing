using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyShot.Audio;

/// <summary>
/// Decodes a sound pack folder into a <see cref="SoundBank"/> at a target sample rate.
/// <para>Pack layout: <c>Sounds/&lt;Mode&gt;/*.mp3|*.wav</c>. Files whose name starts with
/// <c>space</c> or <c>enter</c> are used for the heavy/strong shots respectively.</para>
/// </summary>
public static class SoundLoader
{
    private static readonly string[] Extensions = [".wav", ".mp3", ".aiff", ".aif", ".wma", ".m4a"];

    /// <summary>Anything quieter than this (~-50 dBFS) at the start/end counts as silence.</summary>
    internal const float SilenceThreshold = 0.003f;

    public static IReadOnlyList<string> AvailableModes(string soundsRoot)
    {
        if (!Directory.Exists(soundsRoot)) return [];
        return Directory.EnumerateDirectories(soundsRoot)
            .Where(d => Directory.EnumerateFiles(d).Any(IsAudioFile))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static SoundBank LoadPack(string folder, int targetSampleRate)
    {
        var normal = new List<SoundSample>();
        var heavy = new List<SoundSample>();
        var strong = new List<SoundSample>();

        foreach (var file in Directory.EnumerateFiles(folder).Where(IsAudioFile).Order(StringComparer.OrdinalIgnoreCase))
        {
            SoundSample sample;
            try
            {
                sample = Decode(file, targetSampleRate);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                continue; // one corrupt file must not take down the whole pack
            }

            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("space", StringComparison.OrdinalIgnoreCase)) heavy.Add(sample);
            else if (name.StartsWith("enter", StringComparison.OrdinalIgnoreCase)) strong.Add(sample);
            else normal.Add(sample);
        }

        if (normal.Count == 0) normal.AddRange(heavy.Concat(strong));
        if (normal.Count == 0) throw new InvalidOperationException($"No playable sounds found in '{folder}'.");
        return new SoundBank(targetSampleRate, normal, heavy, strong);
    }

    public static SoundSample Decode(string path, int targetSampleRate)
    {
        using var reader = new AudioFileReader(path);
        ISampleProvider provider = reader.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(reader),
            2 => reader,
            _ => throw new NotSupportedException("Only mono and stereo sound files are supported."),
        };
        if (provider.WaveFormat.SampleRate != targetSampleRate)
            provider = new WdlResamplingSampleProvider(provider, targetSampleRate);

        var data = new List<float>(targetSampleRate * 2 * 3);
        var buffer = new float[targetSampleRate * 2 / 10];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            data.AddRange(buffer.AsSpan(0, read));

        return new SoundSample(Path.GetFileNameWithoutExtension(path), TrimSilence(data.ToArray(), targetSampleRate));
    }

    /// <summary>
    /// Removes leading and trailing silence. MP3 encoders pad the start with ~25-50 ms of
    /// silence; cutting it is the single biggest win for perceived keystroke latency.
    /// </summary>
    internal static float[] TrimSilence(float[] stereo, int sampleRate)
    {
        int frames = stereo.Length / 2;
        int first = 0;
        while (first < frames && Math.Abs(stereo[first * 2]) < SilenceThreshold && Math.Abs(stereo[first * 2 + 1]) < SilenceThreshold)
            first++;
        int last = frames - 1;
        while (last > first && Math.Abs(stereo[last * 2]) < SilenceThreshold && Math.Abs(stereo[last * 2 + 1]) < SilenceThreshold)
            last--;

        if (first >= last) return stereo; // all silent: leave untouched

        // Keep 1 ms before the transient so its attack is not chopped.
        first = Math.Max(0, first - sampleRate / 1000);
        int count = last - first + 1;
        var trimmed = new float[count * 2];
        Array.Copy(stereo, first * 2, trimmed, 0, trimmed.Length);

        // Short fade-out so the trimmed tail never ends with a click.
        int fade = Math.Min(count, sampleRate / 200);
        for (int i = 0; i < fade; i++)
        {
            float g = i / (float)fade;
            int f = count - 1 - i;
            trimmed[f * 2] *= g;
            trimmed[f * 2 + 1] *= g;
        }
        return trimmed;
    }

    private static bool IsAudioFile(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
