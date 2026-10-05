using Hoi4ModOverlay.Services;

namespace Hoi4ModOverlay.Models;

public sealed class ModInfo
{
    public int Order { get; init; }
    public required string Name { get; init; }
    public required string WorkshopId { get; init; }
    public string? SupportedVersion { get; init; }
    public required string SummaryZh { get; init; }
    public string? DetailZh { get; init; }
    public string? SourceUrl { get; init; }
    public string? ContentPath { get; init; }
    public bool HasSummary { get; init; }

    public string OrderLabel => Order.ToString("00");
    public string VersionLabel => string.IsNullOrWhiteSpace(SupportedVersion) ? "版本未知" : $"支持 {SupportedVersion}";
    public string WorkshopLabel => ModSummaryEnrichmentService.IsSteamWorkshopId(WorkshopId) ? $"ID {WorkshopId}" : "本地模组";
}
