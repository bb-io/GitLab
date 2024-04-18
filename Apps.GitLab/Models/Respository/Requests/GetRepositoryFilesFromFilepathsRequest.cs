﻿using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Gitlab.Models.Respository.Requests;

public class GetRepositoryFilesFromFilepathsRequest
{
    [Display("File paths array", Description = "e.g. \"TestFolder/TestFile.txt\"")]
    public List<string> FilePaths { get; set; }
}