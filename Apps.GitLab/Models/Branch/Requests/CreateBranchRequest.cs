﻿using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.Branch.Requests;

public class CreateBranchRequest
{
    [Display("Base branch name")]
    [DataSource(typeof(BranchDataHandler))]
    public string BaseBranchName { get; set; }

    [Display("New branch name")]
    public string NewBranchName { get; set; }
}