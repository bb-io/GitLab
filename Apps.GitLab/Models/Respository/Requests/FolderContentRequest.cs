﻿using Blackbird.Applications.Sdk.Common;
namespace Apps.Gitlab.Models.Respository.Requests;

public class FolderContentRequest
{
    public FolderContentRequest()
    {
    }

    public FolderContentRequest(string? path, bool? includeSubfolders)
    {
        Path = path;
        IncludeSubfolders = includeSubfolders;
    }

    [Display("Folder path (e.g. \"Folder1/Folder2\")")]
    public string? Path { get; set; }

    [Display("Include subfolders")]
    public bool? IncludeSubfolders { get; set; }
}