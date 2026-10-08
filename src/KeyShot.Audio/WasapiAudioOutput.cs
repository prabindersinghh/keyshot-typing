using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace KeyShot.Audio;

/// <summary>
/// WASAPI shared-mode, event-driven output on the default render device.
/// The stream is opened in the device's own mix format so Windows does not
/// insert an extra resampler (and its buffering) between KeyShot and the speakers.
/// </summary>
public sealed class WasapiAudioOutput : IAudioOutput, IMMNotificationClient
{
    /// <summary>KSDATAFORMAT_SUBTYPE_IEEE_FLOAT.</summary>
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice _device;
    private readonly WaveFormat _format;
    private readonly int _latencyMs;
    private WasapiOut? _out;
    private int _lost;

    public WasapiAudioOutput(int latencyMs)
    {
        _latencyMs = latencyMs;
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        DeviceName = _device.FriendlyName;

        var mix = _device.AudioClient.MixFormat;
        bool isFloat32 = mix.BitsPerSample == 32 &&
            (mix.Encoding == WaveFormatEncoding.IeeeFloat ||
             (mix is WaveFormatExtensible ext && ext.SubFormat == IeeeFloatSubFormat));
        _format = isFloat32 ? mix : WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, Math.Min(mix.Channels, 2));
        SampleRate = _format.SampleRate;
        Channels = _format.Channels;

        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public string DeviceName { get; }
    public int SampleRate { get; }
    public int Channels { get; }

    public event Action? DeviceLost;

    public void Start(ShotMixer mixer)
    {
        _out = new WasapiOut(_device, AudioClientShareMode.Shared, useEventSync: true, _latencyMs);
        _out.PlaybackStopped += (_, e) => { if (e.Exception != null) RaiseLost(); };
        _out.Init(new MixerWaveProvider(mixer, _format));
        _out.Play();
    }

    public void Dispose()
    {
        try { _enumerator.UnregisterEndpointNotificationCallback(this); } catch (COMException) { }
        try { _out?.Stop(); } catch (COMException) { }
        _out?.Dispose();
        _out = null;
        _device.Dispose();
        _enumerator.Dispose();
    }

    private void RaiseLost()
    {
        // Notify once; the engine rebuilds a fresh output on a worker thread.
        if (Interlocked.Exchange(ref _lost, 1) == 0) DeviceLost?.Invoke();
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia) RaiseLost();
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        if (newState != DeviceState.Active && string.Equals(deviceId, _device.ID, StringComparison.OrdinalIgnoreCase))
            RaiseLost();
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId)
    {
        if (string.Equals(deviceId, _device.ID, StringComparison.OrdinalIgnoreCase)) RaiseLost();
    }

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) { }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    /// <summary>Bridges the float mixer to WASAPI's byte buffers without copying.</summary>
    private sealed class MixerWaveProvider(ShotMixer mixer, WaveFormat format) : IWaveProvider
    {
        public WaveFormat WaveFormat => format;

        public int Read(byte[] buffer, int offset, int count)
        {
            var floats = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, count));
            mixer.Read(floats);
            return count; // always "full": returning 0 would end playback
        }
    }
}
