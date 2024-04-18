﻿using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.Commit.Requests;

public class DeleteFileRequest
{
    [Display("File path")]
    public string FilePath { get; set; }

    [Display("Commit message")]
    public string CommitMessage { get; set; }
}