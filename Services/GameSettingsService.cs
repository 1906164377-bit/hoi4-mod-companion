using System.IO;
using System.Text.RegularExpressions;

namespace Hoi4ModOverlay.Services;

public sealed class GameSettingsService
{
    private readonly string _userRoot;

    public GameSettingsService()
    {
        _userRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            "Hearts of Iron IV");
    }

    public void EnsureBorderlessFullscreen()
    {
        PatchLegacySettings();
        PatchPdxSettings();
    }

    private void PatchLegacySettings()
    {
        var path = Path.Combine(_userRoot, "settings.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var updated = Regex.Replace(
            text,
            @"(?m)^(\s*)fullScreen\s*=\s*(yes|no)\s*$",
            "$1fullScreen=no",
            RegexOptions.IgnoreCase);
        updated = Regex.Replace(
            updated,
            @"(?m)^(\s*)borderless\s*=\s*(yes|no)\s*$",
            "$1borderless=yes",
            RegexOptions.IgnoreCase);

        if (!string.Equals(text, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(path, updated);
        }
    }

    private void PatchPdxSettings()
    {
        var path = Path.Combine(_userRoot, "pdx_settings.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var updated = Regex.Replace(
            text,
            @"(?s)(""display_mode""\s*=\s*\{.*?value\s*=\s*"")[^""]*("")",
            "$1borderless_fullscreen$2",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

        if (!string.Equals(text, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(path, updated);
        }
    }
}
