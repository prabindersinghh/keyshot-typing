using KeyShot.Audio;
using KeyShot.Core;

namespace KeyShot.Tests;

public class AudioEngineTests
{
    private sealed class FakeOutput : IAudioOutput
    {
        public string DeviceName => "Fake Speakers";
        public int SampleRate => 48000;
        public int Channels => 2;
        public ShotMixer? Mixer { get; private set; }
        public bool Disposed { get; private set; }
        public event Action? DeviceLost;

        public void Start(ShotMixer mixer) => Mixer = mixer;
        public void Dispose() => Disposed = true;
        public void Lose() => DeviceLost?.Invoke();
    }

    private sealed class Rig
    {
        public List<FakeOutput> Outputs { get; } = [];
        public int BankLoads { get; private set; }
        public bool FailOutput { get; set; }
        public bool FailBank { get; set; }
        public AudioEngine Engine { get; }

        public Rig()
        {
            Engine = new AudioEngine(
                _ =>
                {
                    if (FailOutput) throw new InvalidOperationException("no device");
                    var o = new FakeOutput();
                    Outputs.Add(o);
                    return o;
                },
                (_, rate) =>
                {
                    if (FailBank) throw new FileNotFoundException("no sounds");
                    BankLoads++;
                    return new SoundBank(rate, [ShotMixerTests.Tone()]);
                });
        }

        public FakeOutput Last => Outputs[^1];
    }

    [Fact]
    public void Starts_stopped()
    {
        var rig = new Rig();
        Assert.Equal(AudioEngineState.Stopped, rig.Engine.State);
        Assert.Empty(rig.Outputs);
    }

    [Fact]
    public void Start_opens_device_and_fire_reaches_mixer()
    {
        var rig = new Rig();
        Assert.True(rig.Engine.Start());
        Assert.Equal(AudioEngineState.Running, rig.Engine.State);
        Assert.Equal("Fake Speakers", rig.Engine.DeviceName);

        rig.Engine.Fire(ShotKind.Normal);
        Assert.Equal(1, rig.Last.Mixer!.PendingTriggers);
    }

    [Fact]
    public void Start_is_idempotent()
    {
        var rig = new Rig();
        rig.Engine.Start();
        rig.Engine.Start();
        Assert.Single(rig.Outputs);
    }

    [Fact]
    public void Stop_releases_device_and_fire_becomes_noop()
    {
        var rig = new Rig();
        rig.Engine.Start();
        var output = rig.Last;
        rig.Engine.Stop();

        Assert.True(output.Disposed);
        Assert.Equal(AudioEngineState.Stopped, rig.Engine.State);
        rig.Engine.Fire(ShotKind.Normal); // must not throw
    }

    [Fact]
    public void Device_failure_faults_instead_of_throwing()
    {
        var rig = new Rig { FailOutput = true };
        Assert.False(rig.Engine.Start());
        Assert.Equal(AudioEngineState.Faulted, rig.Engine.State);
        Assert.IsType<InvalidOperationException>(rig.Engine.LastError);
        rig.Engine.Fire(ShotKind.Strong); // still safe
    }

    [Fact]
    public void Missing_sounds_fault_and_release_the_device()
    {
        var rig = new Rig { FailBank = true };
        Assert.False(rig.Engine.Start());
        Assert.Equal(AudioEngineState.Faulted, rig.Engine.State);
        Assert.True(rig.Last.Disposed);
    }

    [Fact]
    public void Recovers_after_fault()
    {
        var rig = new Rig { FailOutput = true };
        rig.Engine.Start();
        rig.FailOutput = false;
        Assert.True(rig.Engine.Start());
        Assert.Equal(AudioEngineState.Running, rig.Engine.State);
        Assert.Null(rig.Engine.LastError);
    }

    [Fact]
    public void Decoded_banks_are_cached_across_restarts()
    {
        var rig = new Rig();
        rig.Engine.Start();
        rig.Engine.Restart();
        rig.Engine.Stop();
        rig.Engine.Start();
        Assert.Equal(1, rig.BankLoads);
        Assert.Equal(3, rig.Outputs.Count);
    }

    [Fact]
    public void Volume_change_applies_without_restart()
    {
        var rig = new Rig();
        rig.Engine.Start();
        rig.Engine.Configure(10, 0, "Shotgun", 10);
        Assert.Single(rig.Outputs);
    }

    [Fact]
    public void Mode_change_restarts_with_new_pack()
    {
        var rig = new Rig();
        rig.Engine.Configure(80, 35, "Shotgun", 10);
        rig.Engine.Start();
        rig.Engine.Configure(80, 35, "Pistol", 10);
        Assert.Equal(2, rig.Outputs.Count);
        Assert.Equal(2, rig.BankLoads);
        Assert.Equal(AudioEngineState.Running, rig.Engine.State);
    }

    [Fact]
    public async Task Lost_device_is_reopened_automatically()
    {
        var rig = new Rig();
        rig.Engine.Start();
        rig.Last.Lose();

        for (int i = 0; i < 50 && rig.Outputs.Count < 2; i++) await Task.Delay(50);

        Assert.Equal(2, rig.Outputs.Count);
        Assert.True(rig.Outputs[0].Disposed);
        Assert.Equal(AudioEngineState.Running, rig.Engine.State);
    }

    [Fact]
    public void Dispose_is_idempotent_and_final()
    {
        var rig = new Rig();
        rig.Engine.Start();
        rig.Engine.Dispose();
        rig.Engine.Dispose();
        Assert.False(rig.Engine.Start());
        Assert.True(rig.Last.Disposed);
    }

    [Fact]
    public void State_changes_are_reported()
    {
        var rig = new Rig();
        int events = 0;
        rig.Engine.StateChanged += (_, _) => events++;
        rig.Engine.Start();
        rig.Engine.Stop();
        Assert.Equal(2, events);
    }

    [Fact]
    public void Throwing_state_listener_does_not_break_engine()
    {
        var rig = new Rig();
        rig.Engine.StateChanged += (_, _) => throw new Exception("ui bug");
        Assert.True(rig.Engine.Start());
    }
}
