using Blackbird.Applications.Sdk.Common;

namespace Apps.GitLab.Webhooks.Payloads;

public class PushCommitOutput
{
    public string Hash { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    [Display("Author name")]
    public string AuthorName { get; set; } = string.Empty;

    [Display("Author email")]
    public string AuthorEmail { get; set; } = string.Empty;
}
