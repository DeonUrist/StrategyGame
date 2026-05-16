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
    public bool ResourceIconsVisible { get; set; } = true;
    public AnimationSpeedSetting AnimationSpeed { get; set; } = AnimationSpeedSetting.Fast;
    public KeyBindingSettings KeyBindings { get; set; } = new();

    public static PresentationSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PresentationSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<PresentationSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                           ?? new PresentationSettings();
            settings.KeyBindings ??= new KeyBindingSettings();
            settings.KeyBindings.OpenMenuPrimary = (int)Godot.Key.Escape;
            return settings;
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

public sealed class KeyBindingSettings
{
    public int OpenMenuPrimary { get; set; } = (int)Godot.Key.Escape;
    public int OpenMenuSecondary { get; set; }
    public int RecenterPrimary { get; set; } = (int)Godot.Key.Space;
    public int RecenterSecondary { get; set; }
}
