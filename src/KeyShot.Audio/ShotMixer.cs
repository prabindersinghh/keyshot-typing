using System.Collections.Concurrent;
using KeyShot.Core;

namespace KeyShot.Audio;

/// <summary>
/// Real-time polyphonic sampler. <see cref="Trigger"/> is called from the keyboard
/// hook thread and only enqueues; all voice management and mixing happens inside
/// <see cref="Read"/> on the audio thread, so the two never contend for a lock.
/// </summary>
public sealed class ShotMixer
{
    public const int MaxVoices = 32;
    private const int MaxPending = 64;

    private readonly ConcurrentQueue<ShotKind> _pending = new();
    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly SampleSelector _selector;
    private volatile SoundBank _bank;
    private volatile float _master = 0.64f;
    private volatile float _variation = 0.35f;
    private long _serial;
    private int _activeVoices;

    public ShotMixer(int sampleRate, int channels, SoundBank bank, Random? random = null)
    {
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        SampleRate = sampleRate;
        Channels = channels;
        _bank = bank;
        _selector = new SampleSelector(random);
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public int ActiveVoices => Volatile.Read(ref _activeVoices);
    public int PendingTriggers => _pending.Count;
    public SoundBank Bank => _bank;

    /// <summary>Master volume 0-100, mapped to a perceptual (squared) gain curve.</summary>
    public void SetVolume(int volume)
    {
        float v = Math.Clamp(volume, 0, 100) / 100f;
        _master = v * v;
    }

    /// <summary>Pitch/volume randomization amount 0-100.</summary>
    public void SetVariation(int variation) => _variation = Math.Clamp(variation, 0, 100) / 100f;

    public void SetBank(SoundBank bank) => _bank = bank;

    /// <summary>Queue a shot. Lock-free, never blocks, safe from any thread.</summary>
    public void Trigger(ShotKind kind)
    {
        // Bounded so a stalled audio device can never grow memory without limit.
        if (_pending.Count < MaxPending) _pending.Enqueue(kind);
    }

    /// <summary>Fill <paramref name="buffer"/> (interleaved, <see cref="Channels"/> wide). Never throws.</summary>
    public void Read(Span<float> buffer)
    {
        buffer.Clear();
        try
        {
            StartPendingVoices();

            int frames = buffer.Length / Channels;
            int active = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active) continue;
                MixVoice(ref _voices[i], buffer, frames);
                if (_voices[i].Active) active++;
            }
            Volatile.Write(ref _activeVoices, active);

            float master = _master;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = SoftClip(buffer[i] * master);
        }
        catch
        {
            buffer.Clear(); // audio problems must never escalate
        }
    }

    private void StartPendingVoices()
    {
        while (_pending.TryDequeue(out var kind))
        {
            var bank = _bank;
            var (pool, rate, gain) = kind switch
            {
                ShotKind.Heavy when bank.Heavy.Count > 0 => (bank.Heavy, 1.0, 0.9f),
                ShotKind.Heavy => (bank.Normal, 0.84, 0.95f),
                ShotKind.Strong when bank.Strong.Count > 0 => (bank.Strong, 1.0, 1.0f),
                ShotKind.Strong => (bank.Normal, 0.74, 1.05f),
                _ => (bank.Normal, 1.0, 0.8f),
            };

            var sample = pool[_selector.Next(pool.Count)];
            float variation = _variation;
            if (variation > 0)
            {
                double semitones = _selector.NextSigned() * 2.0 * variation;
                rate *= Math.Pow(2.0, semitones / 12.0);
                gain *= 1f - variation * 0.3f * (float)((_selector.NextSigned() + 1) * 0.5);
            }

            ref var slot = ref _voices[FindFreeOrOldest()];
            slot = new Voice
            {
                Data = sample.Data,
                Frames = sample.Frames,
                Position = 0,
                Rate = rate,
                Gain = gain,
                Serial = ++_serial,
                Active = true,
            };
        }
    }

    private int FindFreeOrOldest()
    {
        int oldest = 0;
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].Active) return i;
            if (_voices[i].Serial < _voices[oldest].Serial) oldest = i;
        }
        return oldest; // voice stealing: the oldest shot is deep in its quiet tail anyway
    }

    private void MixVoice(ref Voice v, Span<float> buffer, int frames)
    {
        float[] d = v.Data;
        int lastFrame = v.Frames - 1;
        double pos = v.Position;
        double rate = v.Rate;
        float g = v.Gain;
        int ch = Channels;

        for (int f = 0; f < frames; f++)
        {
            int i = (int)pos;
            if (i >= lastFrame)
            {
                v.Active = false;
                return;
            }

            // Linear interpolation gives cheap, artifact-free pitch shifting.
            float frac = (float)(pos - i);
            int j = i * 2;
            float l = d[j] + (d[j + 2] - d[j]) * frac;
            float r = d[j + 1] + (d[j + 3] - d[j + 1]) * frac;

            int o = f * ch;
            if (ch == 1)
            {
                buffer[o] += (l + r) * 0.5f * g;
            }
            else
            {
                buffer[o] += l * g;
                buffer[o + 1] += r * g;
            }
            pos += rate;
        }
        v.Position = pos;
    }

    /// <summary>Transparent below 0.8, smoothly saturates above so stacked shots never hard-clip.</summary>
    internal static float SoftClip(float x)
    {
        float a = Math.Abs(x);
        if (a <= 0.8f) return x;
        float y = 0.8f + 0.2f * MathF.Tanh((a - 0.8f) / 0.2f);
        return x < 0 ? -y : y;
    }

    private struct Voice
    {
        public float[] Data;
        public int Frames;
        public double Position;
        public double Rate;
        public float Gain;
        public long Serial;
        public bool Active;
    }
}
