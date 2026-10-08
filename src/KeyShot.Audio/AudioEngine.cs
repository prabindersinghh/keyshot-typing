using KeyShot.Core;

namespace KeyShot.Audio;

public enum AudioEngineState
{
    Stopped,
    Running,
    Faulted,
}

/// <summary>
/// Owns the device stream, the decoded sound bank and the mixer, and exposes
/// the whole thing as an <see cref="IKeyEffect"/>. No public member throws:
/// failures move the engine to <see cref="AudioEngineState.Faulted"/> instead.
/// </summary>
public sealed class AudioEngine : IKeyEffect, IDisposable
{
    private readonly object _gate = new();
    private readonly AudioOutputFactory _outputFactory;
    private readonly Func<string, int, SoundBank> _bankLoader;
    private readonly Dictionary<(string, int), SoundBank> _bankCache = new();
    private IAudioOutput? _output;
    private volatile ShotMixer? _mixer;
    private int _volume = 80;
    private int _variation = 35;
    private int _latencyMs = 10;
    private string _mode = "Shotgun";
    private bool _disposed;

    public AudioEngine(string soundsRoot)
        : this(latency => new WasapiAudioOutput(latency),
               (mode, rate) => SoundLoader.LoadPack(Path.Combine(soundsRoot, mode), rate))
    {
    }

    public AudioEngine(AudioOutputFactory outputFactory, Func<string, int, SoundBank> bankLoader)
    {
        _outputFactory = outputFactory;
        _bankLoader = bankLoader;
    }

    public string Name => "Sound";

    public AudioEngineState State { get; private set; } = AudioEngineState.Stopped;

    public Exception? LastError { get; private set; }

    public string DeviceName { get; private set; } = "—";

    public int ActiveVoices => _mixer?.ActiveVoices ?? 0;

    /// <summary>Raised (on an arbitrary thread) after every state or device change.</summary>
    public event EventHandler? StateChanged;

    public void Configure(int volume, int variation, string mode, int latencyMs)
    {
        bool restart;
        lock (_gate)
        {
            _volume = volume;
            _variation = variation;
            _mixer?.SetVolume(volume);
            _mixer?.SetVariation(variation);
            restart = State == AudioEngineState.Running &&
                      (!string.Equals(mode, _mode, StringComparison.OrdinalIgnoreCase) || latencyMs != _latencyMs);
            _mode = mode;
            _latencyMs = latencyMs;
        }
        if (restart) Restart();
    }

    public bool Start()
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (State == AudioEngineState.Running) return true;

            try
            {
                var output = _outputFactory(_latencyMs);
                try
                {
                    var bank = GetBank(_mode, output.SampleRate);
                    var mixer = new ShotMixer(output.SampleRate, output.Channels, bank);
                    mixer.SetVolume(_volume);
                    mixer.SetVariation(_variation);
                    output.DeviceLost += OnDeviceLost;
                    output.Start(mixer);

                    _output = output;
                    _mixer = mixer;
                    DeviceName = output.DeviceName;
                    LastError = null;
                    State = AudioEngineState.Running;
                }
                catch
                {
                    output.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                LastError = ex;
                State = AudioEngineState.Faulted;
            }
        }
        RaiseStateChanged();
        return State == AudioEngineState.Running;
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopCore();
            if (State != AudioEngineState.Faulted) State = AudioEngineState.Stopped;
        }
        RaiseStateChanged();
    }

    public bool Restart()
    {
        lock (_gate)
        {
            StopCore();
            State = AudioEngineState.Stopped;
        }
        return Start();
    }

    /// <summary>Hook-thread entry point: one volatile read and a lock-free enqueue.</summary>
    public void Fire(ShotKind kind) => _mixer?.Trigger(kind);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            StopCore();
            State = AudioEngineState.Stopped;
        }
    }

    private SoundBank GetBank(string mode, int sampleRate)
    {
        if (!_bankCache.TryGetValue((mode, sampleRate), out var bank))
        {
            bank = _bankLoader(mode, sampleRate);
            _bankCache[(mode, sampleRate)] = bank;
        }
        return bank;
    }

    private void StopCore()
    {
        _mixer = null;
        var output = _output;
        _output = null;
        if (output == null) return;
        output.DeviceLost -= OnDeviceLost;
        try { output.Dispose(); } catch { /* device may already be gone */ }
    }

    private void OnDeviceLost()
    {
        // Rebuild off the notification thread (COM forbids blocking calls there).
        _ = Task.Run(async () =>
        {
            await Task.Delay(300); // let Windows finish switching devices
            if (!_disposed) Restart();
        });
    }

    private void RaiseStateChanged()
    {
        try { StateChanged?.Invoke(this, EventArgs.Empty); } catch { /* UI handlers must not break audio */ }
    }
}
