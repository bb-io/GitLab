using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.Gitlab.DataSourceHandlers.EnumHandlers;

public class RepoVisibilityDataHandler : IStaticDataSourceHandler
{
    public Dictionary<string, string> GetData()
    {
        return new()
        {
            {"private", "Private"},
            {"internal", "Internal"},
            {"public", "Public"},
        };
    }
}
