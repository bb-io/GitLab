using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Models.Commit.Requests;

public class AddedOrModifiedHoursRequest
{
    [Display("Last X hours", Description = "List changes in specified hours amount")]
    public int Hours { get; set; }
}