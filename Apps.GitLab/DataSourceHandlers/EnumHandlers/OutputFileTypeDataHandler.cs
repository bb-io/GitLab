using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.GitLab.DataSourceHandlers.EnumHandlers;

public class OutputFileTypeDataHandler : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return
        [
            new(OutputFileTypes.Original, "Original"),
            new(OutputFileTypes.Xliff1, "XLIFF v1.2"),
            new(OutputFileTypes.Xliff2, "XLIFF v2.2 (interoperable XLIFF)")
        ];
    }
}
