using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Diagnostics;

public class DiagnoseEventStreamResponse
{
    [Display("Summary")]
    public string Summary { get; set; } = string.Empty;

    [Display("Events in time window", Description = "Returned event records whose push timestamps fall inside the exact inclusive window")]
    public int EventCount { get; set; }

    [Display("Returned event count", Description = "All event records collected from the broader date-based API query")]
    public int ReturnedEventCount { get; set; }

    [Display("Collection complete", Description = "Whether pagination and event reporting completed within the diagnostic limits")]
    public bool CollectionComplete { get; set; }

    [Display("Diagnostic report", Description = "JSON containing the exact API query, UTC time window, event payload evidence and request results")]
    public string ReportJson { get; set; } = string.Empty;
}
