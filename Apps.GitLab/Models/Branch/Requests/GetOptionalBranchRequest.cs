using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.Gitlab.Models.Branch.Requests
{
    public class GetOptionalBranchRequest
    {
        [Display("Branch name")]
        [DataSource(typeof(BranchDataHandler))]
        public string? Name { get; set; }
    }
}
