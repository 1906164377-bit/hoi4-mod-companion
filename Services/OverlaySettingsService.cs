using System.IO;
using System.Text.Json;

namespace Hoi4ModOverlay.Services;

public sealed class OverlaySettingsService
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "overlay_settings.json");

    public OverlayPositionSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new OverlayPositionSettings();
            }

            var settings = JsonSerializer.Deserialize<OverlayPositionSettings>(File.ReadAllText(_path));
            if (settings is null)
            {
                return new OverlayPositionSettings();
            }

            settings.WindowWidth = Math.Clamp(settings.WindowWidth, 460, 1400);
            settings.WindowHeight = Math.Clamp(settings.WindowHeight, 360, 1200);
            return settings;
        }
        catch
        {
            return new OverlayPositionSettings();
        }
    }

    public void Save(OverlayPositionSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            settings.WindowWidth = Math.Clamp(settings.WindowWidth, 460, 1400);
            settings.WindowHeight = Math.Clamp(settings.WindowHeight, 360, 1200);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, true);
        }
        catch
        {
            // A failed preference save must never break the companion window.
        }
    }
}

public sealed class OverlayPositionSettings
{
    // Legacy game-relative values are retained for backward compatibility with older settings files.
    public bool HasCustomPosition { get; set; }
    public double CenterXRatio { get; set; } = 0.5;
    public double TopRatio { get; set; } = 0.0015;

    public bool HasStandaloneBounds { get; set; }
    public double WindowLeft { get; set; } = 120;
    public double WindowTop { get; set; } = 120;
    public double WindowWidth { get; set; } = 560;
    public double WindowHeight { get; set; } = 650;
}
