using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.DataSourceHandlers.EnumHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GitLab.Models.Respository.Requests
{
    public class FolderContentWithTypeRequest : FolderContentRequest
    {
        [Display("Content type")]
        [StaticDataSource(typeof(ContentTypeDataHandler))]
        public string? ContentType { get; set; }
    }
}
