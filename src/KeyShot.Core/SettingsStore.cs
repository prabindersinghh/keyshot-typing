using System.Text.Json;

namespace KeyShot.Core;

/// <summary>Loads and saves <see cref="KeyShotSettings"/> as JSON. Never throws on bad files.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsStore(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyShot", "settings.json");
    }

    public string Path { get; }

    public KeyShotSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new KeyShotSettings();
            var json = File.ReadAllText(Path);
            var settings = JsonSerializer.Deserialize<KeyShotSettings>(json, Options);
            return (settings ?? new KeyShotSettings()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new KeyShotSettings();
        }
    }

    public bool Save(KeyShotSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings.Normalize(), Options));
            File.Move(tmp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
