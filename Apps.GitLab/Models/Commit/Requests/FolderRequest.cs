using Blackbird.Applications.Sdk.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apps.GitLab.Models.Commit.Requests
{
    public class FolderRequest
    {
        [Display("Path pattern", Description = "Use the forward slash '/' to represent directory separator. Use '*' to represent wildcards in file and directory names. Use '**' to represent arbitrary directory depth.")]
        public string? FolderPath { get; set; }
    }
}
