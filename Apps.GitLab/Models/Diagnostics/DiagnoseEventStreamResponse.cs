using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Diagnostics;

public class DiagnoseEventStreamResponse
{
    [Display("Summary")]
    public string Summary { get; set; } = string.Empty;

    [Display("Global events in time window", Description = "Global feed records whose push timestamps fall inside the exact inclusive window, across all repositories")]
    public int EventCount { get; set; }

    [Display("Global returned event count", Description = "All event records collected from the global date-based API query")]
    public int ReturnedEventCount { get; set; }

    [Display("Global repository events in time window", Description = "Global feed records for the selected repository inside the exact window")]
    public int GlobalRepositoryEventCount { get; set; }

    [Display("Repository events in time window", Description = "Repository feed records inside the exact window")]
    public int RepositoryEventCount { get; set; }

    [Display("Repository returned event count", Description = "All records collected from the repository date-based API query")]
    public int RepositoryReturnedEventCount { get; set; }

    [Display("Collection complete", Description = "Whether pagination and event reporting completed within the diagnostic limits")]
    public bool CollectionComplete { get; set; }

    [Display("Diagnostic report", Description = "JSON containing the exact API query, UTC time window, event payload evidence and request results")]
    public string ReportJson { get; set; } = string.Empty;
}
