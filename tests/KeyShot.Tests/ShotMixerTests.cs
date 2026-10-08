using KeyShot.Audio;
using KeyShot.Core;

namespace KeyShot.Tests;

public class ShotMixerTests
{
    internal static SoundSample Tone(string name = "tone", int frames = 4800, float amplitude = 0.5f)
    {
        var data = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float v = amplitude * MathF.Sin(i * 0.05f);
            data[i * 2] = v;
            data[i * 2 + 1] = v;
        }
        return new SoundSample(name, data);
    }

    internal static SoundBank Bank(params SoundSample[] samples) =>
        new(48000, samples.Length > 0 ? samples : [Tone()]);

    private static float Peak(ReadOnlySpan<float> s)
    {
        float p = 0;
        foreach (var v in s) p = Math.Max(p, Math.Abs(v));
        return p;
    }

    [Fact]
    public void Idle_mixer_outputs_silence()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        var buffer = new float[960];
        Array.Fill(buffer, 1f); // garbage must be overwritten
        mixer.Read(buffer);
        Assert.Equal(0f, Peak(buffer));
    }

    [Fact]
    public void Trigger_is_heard_in_the_very_next_buffer()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        mixer.SetVolume(100);
        mixer.Trigger(ShotKind.Normal);

        var buffer = new float[480 * 2]; // one 10 ms WASAPI period
        mixer.Read(buffer);

        Assert.True(Peak(buffer) > 0.01f);
        Assert.Equal(1, mixer.ActiveVoices);
    }

    [Fact]
    public void Overlapping_shots_play_simultaneously()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        for (int i = 0; i < 5; i++) mixer.Trigger(ShotKind.Normal);
        mixer.Read(new float[256]);
        Assert.Equal(5, mixer.ActiveVoices);
    }

    [Fact]
    public void Voices_finish_and_free_themselves()
    {
        var mixer = new ShotMixer(48000, 2, Bank(Tone(frames: 100)));
        mixer.SetVariation(0);
        mixer.Trigger(ShotKind.Normal);
        mixer.Read(new float[400 * 2]);
        Assert.Equal(0, mixer.ActiveVoices);
    }

    [Fact]
    public void Voice_count_is_capped()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < ShotMixer.MaxVoices; i++) mixer.Trigger(ShotKind.Normal);
            mixer.Read(new float[64]);
        }
        Assert.Equal(ShotMixer.MaxVoices, mixer.ActiveVoices);
    }

    [Fact]
    public void Pending_queue_is_bounded_when_audio_is_stalled()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        for (int i = 0; i < 10_000; i++) mixer.Trigger(ShotKind.Normal);
        Assert.InRange(mixer.PendingTriggers, 1, 64);
    }

    [Fact]
    public void Zero_volume_is_silent()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        mixer.SetVolume(0);
        mixer.Trigger(ShotKind.Strong);
        var buffer = new float[960];
        mixer.Read(buffer);
        Assert.Equal(0f, Peak(buffer));
    }

    [Fact]
    public void Stacked_full_scale_shots_never_exceed_unity()
    {
        var mixer = new ShotMixer(48000, 2, Bank(Tone(amplitude: 1f)));
        mixer.SetVolume(100);
        for (int i = 0; i < ShotMixer.MaxVoices; i++) mixer.Trigger(ShotKind.Strong);
        var buffer = new float[4800];
        mixer.Read(buffer);
        Assert.InRange(Peak(buffer), 0.5f, 1.0f);
    }

    [Fact]
    public void Soft_clip_is_transparent_at_normal_levels()
    {
        Assert.Equal(0.5f, ShotMixer.SoftClip(0.5f));
        Assert.Equal(-0.3f, ShotMixer.SoftClip(-0.3f));
        Assert.InRange(ShotMixer.SoftClip(10f), 0.99f, 1.0f);
        Assert.InRange(ShotMixer.SoftClip(-10f), -1.0f, -0.99f);
    }

    [Fact]
    public void Works_with_mono_and_surround_devices()
    {
        foreach (var channels in new[] { 1, 2, 6, 8 })
        {
            var mixer = new ShotMixer(48000, channels, Bank());
            mixer.SetVolume(100);
            mixer.Trigger(ShotKind.Normal);
            var buffer = new float[480 * channels];
            mixer.Read(buffer);
            Assert.True(Peak(buffer) > 0.01f, $"no output on {channels} channels");
        }
    }

    [Fact]
    public void Heavy_and_strong_shots_are_pitched_down_without_dedicated_samples()
    {
        // A lower playback rate means the voice lasts longer: count frames until it ends.
        static int Duration(ShotKind kind)
        {
            var mixer = new ShotMixer(48000, 2, Bank(Tone(frames: 1000)));
            mixer.SetVariation(0);
            mixer.Trigger(kind);
            int frames = 0;
            var buffer = new float[2];
            do { mixer.Read(buffer); frames++; } while (mixer.ActiveVoices > 0 && frames < 10_000);
            return frames;
        }

        int normal = Duration(ShotKind.Normal);
        int heavy = Duration(ShotKind.Heavy);
        int strong = Duration(ShotKind.Strong);
        Assert.True(heavy > normal);
        Assert.True(strong > heavy);
    }

    [Fact]
    public void Dedicated_samples_are_used_for_space_and_enter()
    {
        var space = Tone("space", frames: 50);
        var bank = new SoundBank(48000, [Tone("normal", frames: 5000)], heavy: [space]);
        var mixer = new ShotMixer(48000, 2, bank);
        mixer.SetVariation(0);
        mixer.Trigger(ShotKind.Heavy);
        mixer.Read(new float[200 * 2]);
        Assert.Equal(0, mixer.ActiveVoices); // the short "space" sample played, not the long one
    }

    [Fact]
    public void Variation_changes_successive_shots()
    {
        var mixer = new ShotMixer(48000, 2, Bank(), new Random(5));
        mixer.SetVolume(100);
        mixer.SetVariation(100);
        var a = new float[960];
        var b = new float[960];
        mixer.Trigger(ShotKind.Normal);
        mixer.Read(a);
        mixer.Read(new float[48000 * 2]); // let it finish
        mixer.Trigger(ShotKind.Normal);
        mixer.Read(b);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Concurrent_triggers_from_another_thread_are_safe()
    {
        var mixer = new ShotMixer(48000, 2, Bank());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var producer = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested) mixer.Trigger(ShotKind.Normal);
        });
        var buffer = new float[960];
        while (!cts.IsCancellationRequested) mixer.Read(buffer);
        await producer;
        Assert.InRange(mixer.ActiveVoices, 0, ShotMixer.MaxVoices);
    }
}
