using Blackbird.Applications.Sdk.Common.Dictionaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
