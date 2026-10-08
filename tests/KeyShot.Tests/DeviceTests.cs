using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyShot.Audio;
using KeyShot.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Xunit.Abstractions;

namespace KeyShot.Tests;

/// <summary>Runs only when KEYSHOT_DEVICE_TESTS=1 (plays real sound on the default device).</summary>
public sealed class DeviceFactAttribute : FactAttribute
{
    public DeviceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KEYSHOT_DEVICE_TESTS") != "1")
            Skip = "Set KEYSHOT_DEVICE_TESTS=1 to run tests against the real audio device.";
    }
}

public class DeviceTests(ITestOutputHelper output)
{
    private static readonly string SoundsRoot = Path.Combine(AppContext.BaseDirectory, "Sounds");

    /// <summary>
    /// Measures KeyShot's own pipeline on the real device: time from Trigger() until the
    /// mixer hands audible samples to WASAPI, plus the device engine period. Anything after
    /// that (DAC, Bluetooth radio/codec) is hardware latency outside the app's control.
    /// </summary>
    [DeviceFact]
    public void Software_pipeline_latency_is_low()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var mix = device.AudioClient.MixFormat;
        double periodMs = device.AudioClient.DefaultDevicePeriod / 10_000.0;
        output.WriteLine($"Device: {device.FriendlyName} ({mix.SampleRate} Hz, {mix.Channels} ch, engine period {periodMs:F1} ms)");

        var bank = SoundLoader.LoadPack(Path.Combine(SoundsRoot, "Shotgun"), mix.SampleRate);
        var mixer = new ShotMixer(mix.SampleRate, mix.Channels, bank);
        mixer.SetVolume(30);
        mixer.SetVariation(0);
        var probe = new ProbeProvider(mixer, mix);

        using var player = new WasapiOut(device, AudioClientShareMode.Shared, true, 10);
        player.Init(probe);
        player.Play();
        Thread.Sleep(300);

        var pickups = new List<double>();
        for (int i = 0; i < 8; i++)
        {
            Thread.Sleep(150);
            probe.Arm(Stopwatch.GetTimestamp());
            mixer.Trigger(ShotKind.Normal);
            Assert.True(probe.Heard.WaitOne(1000), "mixer never produced the shot");
            pickups.Add(probe.LastPickupMs);
        }
        player.Stop();

        pickups.Sort();
        double median = pickups[pickups.Count / 2];
        output.WriteLine($"Trigger -> samples handed to WASAPI (ms): {string.Join(", ", pickups.Select(p => p.ToString("F1")))}");
        output.WriteLine($"Median pickup {median:F1} ms + one engine period {periodMs:F1} ms ≈ {median + periodMs:F1} ms to the audio driver");
        Assert.True(median + periodMs < 30, "software pipeline latency too high");
    }

    /// <summary>End-to-end smoke test: the shot really appears in the system mix (loopback).</summary>
    [DeviceFact]
    public void Real_device_plays_audible_shot()
    {
        using var engine = new AudioEngine(SoundsRoot);
        engine.Configure(volume: 30, variation: 0, mode: "Shotgun", latencyMs: 10);
        Assert.True(engine.Start(), engine.LastError?.ToString());

        using var capture = new WasapiLoopbackCapture();
        int blockAlign = capture.WaveFormat.BlockAlign;
        var heard = new ManualResetEventSlim();
        bool armed = false;
        capture.DataAvailable += (_, e) =>
        {
            if (!Volatile.Read(ref armed)) return;
            for (int i = 0; i + 4 <= e.BytesRecorded; i += blockAlign)
            {
                if (Math.Abs(BitConverter.ToSingle(e.Buffer, i)) > 0.02f)
                {
                    heard.Set();
                    return;
                }
            }
        };
        capture.StartRecording();
        Thread.Sleep(300);

        Volatile.Write(ref armed, true);
        engine.Fire(ShotKind.Strong);
        bool ok = heard.Wait(1500);

        capture.StopRecording();
        engine.Stop();
        output.WriteLine($"Device: {engine.DeviceName}, heard on loopback: {ok}");
        Assert.True(ok, "shot was not heard on the system loopback");
    }

    /// <summary>Passes mixer audio through and timestamps the first audible buffer after a trigger.</summary>
    private sealed class ProbeProvider(ShotMixer mixer, WaveFormat format) : IWaveProvider
    {
        private long _armedAt;
        public AutoResetEvent Heard { get; } = new(false);
        public double LastPickupMs { get; private set; }
        public WaveFormat WaveFormat => format;

        public void Arm(long timestamp) => Volatile.Write(ref _armedAt, timestamp);

        public int Read(byte[] buffer, int offset, int count)
        {
            var floats = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, count));
            mixer.Read(floats);
            long armed = Volatile.Read(ref _armedAt);
            if (armed != 0)
            {
                foreach (var s in floats)
                {
                    if (Math.Abs(s) > 0.001f)
                    {
                        LastPickupMs = (Stopwatch.GetTimestamp() - armed) * 1000.0 / Stopwatch.Frequency;
                        Volatile.Write(ref _armedAt, 0);
                        Heard.Set();
                        break;
                    }
                }
            }
            return count;
        }
    }
}
