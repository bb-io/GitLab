using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.GitLab.DataSourceHandlers.EnumHandlers
{
    public class ContentTypeDataHandler : IStaticDataSourceHandler
    {
        public Dictionary<string, string> GetData()
        {
            return new()
            {
                {"blob tree", "All"},
                {"blob", "Files"},
                {"tree", "Folders"},
            };
        }
    }
}
