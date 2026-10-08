using KeyShot.Audio;

namespace KeyShot.Tests;

public class SoundLoaderTests
{
    private static readonly string SoundsRoot = Path.Combine(AppContext.BaseDirectory, "Sounds");

    [Fact]
    public void Trim_removes_leading_and_trailing_silence()
    {
        const int rate = 48000;
        var data = new float[(rate + 1000) * 2];
        for (int f = 500; f < 600; f++) data[f * 2] = data[f * 2 + 1] = 0.8f;

        var trimmed = SoundLoader.TrimSilence(data, rate);

        int frames = trimmed.Length / 2;
        Assert.InRange(frames, 100, 100 + rate / 1000 + 1); // signal + 1 ms pre-roll
        Assert.True(Math.Abs(trimmed[(rate / 1000) * 2]) > 0.5f); // transient sits right after the pre-roll
    }

    [Fact]
    public void Trim_leaves_all_silent_audio_alone()
    {
        var data = new float[2000];
        Assert.Same(data, SoundLoader.TrimSilence(data, 48000));
    }

    private static string BundledSample => Directory.GetFiles(Path.Combine(SoundsRoot, "Shotgun"), "*.wav").Order().First();

    [Fact]
    public void Bundled_shotgun_pack_is_discovered_with_variants()
    {
        Assert.Contains("Shotgun", SoundLoader.AvailableModes(SoundsRoot));
        var bank = SoundLoader.LoadPack(Path.Combine(SoundsRoot, "Shotgun"), 48000);
        Assert.True(bank.Normal.Count >= 2, "random selection needs several variants");
    }

    /// <summary>Guards every pack in Sounds/ (including locally added ones): decodable, instant attack.</summary>
    [Fact]
    public void Every_pack_loads_and_attacks_instantly()
    {
        foreach (var mode in SoundLoader.AvailableModes(SoundsRoot))
        {
            var bank = SoundLoader.LoadPack(Path.Combine(SoundsRoot, mode), 48000);
            foreach (var sample in bank.Normal.Concat(bank.Heavy).Concat(bank.Strong))
            {
                int firstLoud = 0;
                while (firstLoud < sample.Frames && Math.Abs(sample.Data[firstLoud * 2]) < 0.01f && Math.Abs(sample.Data[firstLoud * 2 + 1]) < 0.01f)
                    firstLoud++;
                Assert.True(firstLoud < 48 * 3, $"{mode}/{sample.Name}: attack starts {firstLoud / 48.0:F1} ms in");
                Assert.InRange(sample.Frames, 48 * 30, 48000 * 4); // 30 ms .. 4 s
            }
        }
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void Bundled_shotgun_decodes_at_device_rate_with_instant_attack(int rate)
    {
        var bank = SoundLoader.LoadPack(Path.Combine(SoundsRoot, "Shotgun"), rate);
        var sample = bank.Normal[0];

        Assert.Equal(rate, bank.SampleRate);
        Assert.InRange(sample.Frames, rate / 4, rate * 5); // a real, multi-hundred-ms sound

        // The blast must start within ~2 ms of sample start: no encoder padding left.
        int firstLoud = 0;
        while (firstLoud < sample.Frames && Math.Abs(sample.Data[firstLoud * 2]) < 0.01f && Math.Abs(sample.Data[firstLoud * 2 + 1]) < 0.01f)
            firstLoud++;
        Assert.True(firstLoud < rate * 2 / 1000, $"attack starts {firstLoud * 1000.0 / rate:F1} ms in");
    }

    [Fact]
    public void Missing_pack_reports_clearly()
    {
        var dir = Directory.CreateTempSubdirectory("keyshot-empty");
        try
        {
            Assert.Throws<InvalidOperationException>(() => SoundLoader.LoadPack(dir.FullName, 48000));
            Assert.Empty(SoundLoader.AvailableModes(dir.FullName));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Corrupt_files_are_skipped()
    {
        var dir = Directory.CreateTempSubdirectory("keyshot-pack");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "broken.mp3"), "not audio");
            File.Copy(BundledSample, Path.Combine(dir.FullName, "good.wav"));
            var bank = SoundLoader.LoadPack(dir.FullName, 48000);
            Assert.Single(bank.Normal);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Space_and_enter_prefixed_files_feed_dedicated_pools()
    {
        var dir = Directory.CreateTempSubdirectory("keyshot-pack");
        try
        {
            var src = BundledSample;
            File.Copy(src, Path.Combine(dir.FullName, "shot1.wav"));
            File.Copy(src, Path.Combine(dir.FullName, "space-boom.wav"));
            File.Copy(src, Path.Combine(dir.FullName, "enter-pump.wav"));
            var bank = SoundLoader.LoadPack(dir.FullName, 48000);
            Assert.Single(bank.Normal);
            Assert.Single(bank.Heavy);
            Assert.Single(bank.Strong);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
