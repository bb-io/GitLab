using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Diagnostics;

public class DiagnoseFilesModifiedResponse
{
    [Display("Repository ID", Description = "Map directly to Repository ID in Diagnose event stream")]
    public string RepositoryId { get; set; } = string.Empty;

    [Display("Timestamp", Description = "UTC timestamp of the latest confirmed push on the expected branch; map directly to Timestamp in Diagnose event stream. Empty when no such push was found.")]
    public string? Timestamp { get; set; }

    [Display("Summary")]
    public string Summary { get; set; } = string.Empty;

    [Display("Related push count", Description = "Pushes confirmed to contain the target commit within the scanned ranges")]
    public int RelatedPushCount { get; set; }

    [Display("Collection complete", Description = "Whether all planned checks completed within the reported search ranges and limits")]
    public bool CollectionComplete { get; set; }

    [Display("Diagnostic report", Description = "JSON evidence for offline investigation, including requests, changed paths, push data and collection limits")]
    public string ReportJson { get; set; } = string.Empty;
}
