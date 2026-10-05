using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hoi4ModOverlay.Models;

namespace Hoi4ModOverlay.Services;

public sealed class ModCatalogService
{
    private readonly string _userRoot;
    private readonly string _summaryPath;
    private readonly string _generatedSummaryPath;

    public ModCatalogService()
    {
        _userRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            "Hearts of Iron IV");
        _summaryPath = Path.Combine(AppContext.BaseDirectory, "data", "mod_summaries.zh-CN.json");
        _generatedSummaryPath = Path.Combine(AppContext.BaseDirectory, "data", "generated_summaries.zh-CN.json");
    }

    public string DlcLoadPath => Path.Combine(_userRoot, "dlc_load.json");

    public IReadOnlyList<ModInfo> LoadEnabledMods()
    {
        if (!File.Exists(DlcLoadPath))
        {
            return [];
        }

        var summaries = LoadSummaries();
        using var doc = JsonDocument.Parse(File.ReadAllText(DlcLoadPath));
        if (!doc.RootElement.TryGetProperty("enabled_mods", out var enabledMods) || enabledMods.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ModInfo>();
        var order = 1;
        foreach (var item in enabledMods.EnumerateArray())
        {
            var entry = item.GetString();
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var modFile = Path.Combine(_userRoot, entry.Replace('/', Path.DirectorySeparatorChar));
            var info = BuildModInfo(modFile, summaries, order++);
            if (info is not null)
            {
                result.Add(info);
            }
        }

        return result;
    }

    public IReadOnlyList<ModInfo> LoadAllKnownMods()
    {
        var modDir = Path.Combine(_userRoot, "mod");
        if (!Directory.Exists(modDir))
        {
            return [];
        }

        var summaries = LoadSummaries();
        var deduped = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
        var order = 1;

        foreach (var modFile in Directory.EnumerateFiles(modDir, "*.mod", SearchOption.TopDirectoryOnly))
        {
            var info = BuildModInfo(modFile, summaries, order++);
            if (info is null)
            {
                continue;
            }

            var key = ModSummaryEnrichmentService.IsSteamWorkshopId(info.WorkshopId)
                ? $"id:{info.WorkshopId}"
                : $"name:{info.Name.Trim().ToLowerInvariant()}|path:{info.ContentPath}";
            deduped[key] = info;
        }

        return deduped.Values
            .OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private ModInfo? BuildModInfo(string modFile, Dictionary<string, ModSummaryRecord> summaries, int order)
    {
        var descriptor = ParseDescriptor(modFile);
        if (descriptor.Count == 0)
        {
            return null;
        }

        var rawId = descriptor.TryGetValue("remote_file_id", out var remoteId)
            ? remoteId
            : Path.GetFileNameWithoutExtension(modFile);
        var name = descriptor.TryGetValue("name", out var descriptorName)
            ? descriptorName
            : Path.GetFileNameWithoutExtension(modFile);
        var supportedVersion = descriptor.GetValueOrDefault("supported_version");
        var contentPath = ResolveContentPath(descriptor.GetValueOrDefault("path"));

        if (contentPath is not null)
        {
            var innerDescriptor = ParseDescriptor(Path.Combine(contentPath, "descriptor.mod"));
            name = descriptor.GetValueOrDefault("name") ?? innerDescriptor.GetValueOrDefault("name") ?? name;
            rawId = descriptor.GetValueOrDefault("remote_file_id") ?? innerDescriptor.GetValueOrDefault("remote_file_id") ?? rawId;
            supportedVersion = descriptor.GetValueOrDefault("supported_version") ?? innerDescriptor.GetValueOrDefault("supported_version") ?? supportedVersion;
        }

        var summary = FindSummary(summaries, rawId, name);
        var hasSummary = summary is not null;
        var sourceUrl = summary?.Source;
        if (string.IsNullOrWhiteSpace(sourceUrl) && ModSummaryEnrichmentService.IsSteamWorkshopId(rawId))
        {
            sourceUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={rawId}";
        }

        return new ModInfo
        {
            Order = order,
            Name = name,
            WorkshopId = rawId,
            SupportedVersion = supportedVersion,
            SummaryZh = hasSummary ? summary!.Summary : "正在读取这个模组的作者说明…",
            DetailZh = hasSummary ? summary!.Detail : "工具正在从模组本地中文资料或 Steam Workshop 作者说明中自动补全功能介绍。",
            SourceUrl = sourceUrl,
            ContentPath = contentPath,
            HasSummary = hasSummary
        };
    }

    private Dictionary<string, ModSummaryRecord> LoadSummaries()
    {
        var merged = new Dictionary<string, ModSummaryRecord>(StringComparer.OrdinalIgnoreCase);

        // Runtime-generated records are loaded first; curated records always win.
        MergeSummaryFile(merged, _generatedSummaryPath, overwrite: true);
        MergeSummaryFile(merged, _summaryPath, overwrite: true);
        return merged;
    }

    private static void MergeSummaryFile(Dictionary<string, ModSummaryRecord> target, string path, bool overwrite)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ModSummaryRecord>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loaded is null)
            {
                return;
            }

            foreach (var (key, value) in loaded)
            {
                if (overwrite || !target.ContainsKey(key))
                {
                    target[key] = value;
                }
            }
        }
        catch
        {
            // A damaged optional cache must not prevent the overlay from loading.
        }
    }

    private static ModSummaryRecord? FindSummary(Dictionary<string, ModSummaryRecord> summaries, string rawId, string name)
    {
        if (summaries.TryGetValue(rawId, out var byId))
        {
            return byId;
        }

        var nameKey = ModSummaryEnrichmentService.SummaryKeyFor(rawId, name);
        return summaries.TryGetValue(nameKey, out var byName) ? byName : null;
    }

    private string? ResolveContentPath(string? descriptorPath)
    {
        if (string.IsNullOrWhiteSpace(descriptorPath))
        {
            return null;
        }

        var normalized = descriptorPath.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.IsPathRooted(normalized) ? normalized : Path.Combine(_userRoot, normalized);
        return Directory.Exists(full) ? full : null;
    }

    private static Dictionary<string, string> ParseDescriptor(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }

        var text = File.ReadAllText(path);
        foreach (var key in new[] { "name", "path", "remote_file_id", "supported_version", "version" })
        {
            var regex = new Regex($"(?m)^\\s*{Regex.Escape(key)}\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);
            var match = regex.Match(text);
            if (match.Success)
            {
                result[key] = match.Groups[1].Value;
            }
        }

        return result;
    }
}
