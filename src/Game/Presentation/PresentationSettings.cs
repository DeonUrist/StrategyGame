using System.Text.Json;

namespace StrategyGame.Presentation;

public enum AnimationSpeedSetting
{
    Immediate,
    Slow,
    Medium,
    Fast
}

public sealed class PresentationSettings
{
    public int EffectsVolume { get; set; } = 100;
    public int MusicVolume { get; set; } = 100;
    public bool GridVisible { get; set; } = true;
    public AnimationSpeedSetting AnimationSpeed { get; set; } = AnimationSpeedSetting.Fast;

    public static PresentationSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PresentationSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<PresentationSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? new PresentationSettings();
        }
        catch
        {
            return new PresentationSettings();
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));
    }
}
