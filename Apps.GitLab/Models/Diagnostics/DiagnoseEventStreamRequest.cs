using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Diagnostics;

public class DiagnoseEventStreamRequest
{
    [Display("Timestamp", Description = "ISO 8601 timestamp with Z or a timezone offset, for example 2026-11-04T02:57:34.718-03:30")]
    public string Timestamp { get; set; } = string.Empty;

    [Display("Minutes before and after", Description = "Minutes to inspect on each side of the timestamp, from 1 to 1440")]
    public int MinutesBeforeAndAfter { get; set; }
}
