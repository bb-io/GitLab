using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.Gitlab.Models.Commit.Requests;

public class DeleteFileRequest
{
    [Display("File path")]
    [FileDataSource(typeof(GitLabFilePickerDataHandler))]
    public string FilePath { get; set; }

    [Display("Commit message")]
    public string CommitMessage { get; set; }
}
