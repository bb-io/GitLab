using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;

namespace Apps.Gitlab.Models.Respository.Requests;

public class GetFileRequest
{
    [Display("File path")]
    [FileDataSource(typeof(GitLabFilePickerDataHandler))]
    public string FilePath { get; set; }
}
