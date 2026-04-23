using Blackbird.Applications.Sdk.Common;
using GitLabApiClient.Models.Projects.Responses;

namespace Apps.GitLab.Models.Respository.Responses;

public class RepositoryResponse
{
    [Display("Repository ID")]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Display("Path")]
    public string? Path { get; set; }

    [Display("Path with namespace")]
    public string? PathWithNamespace { get; set; }

    [Display("Default branch")]
    public string? DefaultBranch { get; set; }

    public string? Description { get; set; }

    public string? Visibility { get; set; }

    [Display("Web URL")]
    public string? WebUrl { get; set; }

    [Display("HTTP URL to repo")]
    public string? HttpUrlToRepo { get; set; }

    [Display("SSH URL to repo")]
    public string? SshUrlToRepo { get; set; }

    [Display("Created at")]
    public string? CreatedAt { get; set; }

    [Display("Last activity at")]
    public string? LastActivityAt { get; set; }

    public static RepositoryResponse FromProject(Project project)
    {
        return new()
        {
            Id = project.Id.ToString(),
            Name = project.Name,
            Path = project.Path,
            PathWithNamespace = project.PathWithNamespace,
            DefaultBranch = project.DefaultBranch,
            Description = project.Description,
            Visibility = project.Visibility.ToString(),
            WebUrl = project.WebUrl,
            HttpUrlToRepo = project.HttpUrlToRepo,
            SshUrlToRepo = project.SshUrlToRepo,
            CreatedAt = project.CreatedAt,
            LastActivityAt = project.LastActivityAt
        };
    }
}
