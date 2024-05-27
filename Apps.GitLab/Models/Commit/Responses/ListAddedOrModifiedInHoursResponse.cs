using Blackbird.Applications.Sdk.Common;
using GitLabApiClient.Models.Commits.Responses;

namespace Apps.GitLab.Models.Commit.Responses;

public class ListAddedOrModifiedInHoursResponse
{
    public List<AddedOrModifiedFile> Files { get; set; }
}

public class AddedOrModifiedFile
{
    public AddedOrModifiedFile(Diff diff)
    {
        Filename = diff.NewPath;
        IsNewFile = diff.IsNewFile;
    }
    public string Filename { get; set; }

    [Display("Is new file")]
    public bool IsNewFile { get; set; }
}