using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.Gitlab.Models.Respository.Requests;

public class FolderContentRequest
{
    public FolderContentRequest()
    {
    }

    public FolderContentRequest(string? path, bool? includeSubfolders)
    {
        Path = path;
        IncludeSubfolders = includeSubfolders;
    }

    [Display("Folder path (e.g. \"Folder1/Folder2\")")]
    [FileDataSource(typeof(GitLabFolderPickerDataHandler))]
    public string? Path { get; set; }

    [Display("Include subfolders")]
    public bool? IncludeSubfolders { get; set; }
}
