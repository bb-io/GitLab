﻿using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Webhooks.Payloads;

public class FolderInput
{
    [Display("Path pattern", Description = "Use the forward slash '/' to represent directory separator. Use '*' to represent wildcards in file and directory names. Use '**' to represent arbitrary directory depth.")]
    public string? FolderPath { get; set; }
}