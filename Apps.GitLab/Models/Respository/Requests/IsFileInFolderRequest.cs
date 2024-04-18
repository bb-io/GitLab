using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.Respository.Requests;

public class IsFileInFolderRequest
{
    [Display("File path")]
    public string FilePath { get; set; }

    [Display("Folder name")]
    public string FolderName { get; set; }
}