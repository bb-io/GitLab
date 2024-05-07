using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.Gitlab.DataSourceHandlers.EnumHandlers
{
    public class RepoVisibilityDataHandler : IStaticDataSourceHandler
    {
        public Dictionary<string, string> GetData()
        {
            return new()
            {
                {"0", "Private"},
                {"1", "Internal"},
                {"2", "Public"},
            };
        }
    }
}
