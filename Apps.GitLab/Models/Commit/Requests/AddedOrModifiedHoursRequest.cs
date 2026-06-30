using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Requests;

public class AddedOrModifiedHoursRequest
{
    [Display("Last X hours", Description = "Number of hours to search for changes")]
    public int Hours { get; set; }
}
