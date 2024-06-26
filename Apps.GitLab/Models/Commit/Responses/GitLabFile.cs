﻿using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Gitlab.Models.Commit.Responses;

public class GitLabFile
{
    [Display("File path")]
    public string FilePath { get; set; }
    
    public FileReference File { get; set; }
}