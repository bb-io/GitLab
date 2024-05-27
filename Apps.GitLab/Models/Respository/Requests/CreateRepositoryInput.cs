using Apps.Gitlab.DataSourceHandlers;
using Apps.Gitlab.DataSourceHandlers.EnumHandlers;
using Apps.GitLab.Models.Respository.Requests;
using Apps.Gitlab.Models.User.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using GitLabApiClient.Models.Projects.Requests;

namespace Apps.Gitlab.Models.Respository.Requests;

public class CreateRepositoryInput
{
    public string Name { get; set; }

    [Display("User ID")]
    [DataSource(typeof(UsersDataHandler))]
    public string? UserId { get; set; }

    [Display("Default branch")]
    public string? DefaultBranch { get; set; }

    [Display("Namespace ID")]
    public int? NamespaceId { get; set; }

    [Display("Description")]
    public string? Description { get; set; }

    [Display("Enable issues")]
    public bool? EnableIssues { get; set; }

    [Display("Enable merge requests")]
    public bool? EnableMergeRequests { get; set; }

    [Display("Enable jobs")]
    public bool? EnableJobs { get; set; }

    [Display("Enable wiki")]
    public bool? EnableWiki { get; set; }

    [Display("Enable snippets")]
    public bool? EnableSnippets { get; set; }

    [Display("Enable container registry")]
    public bool? EnableContainerRegistry { get; set; }

    [Display("Enable shared runners")]
    public bool? EnableSharedRunners { get; set; }
        
    [Display("Visibility")]
    [StaticDataSource(typeof(RepoVisibilityDataHandler))]
    public string? Visibility { get; set; }

    [Display("Import url")]
    public string? ImportUrl { get; set; }

    [Display("Public jobs")]
    public bool? PublicJobs { get; set; }

    [Display("Only allow merge if pipeline succeeds")]
    public bool? OnlyAllowMergeIfPipelineSucceeds { get; set; }

    [Display("Only allow merge if all discussions are resolved")]
    public bool? OnlyAllowMergeIfAllDiscussionsAreResolved { get; set; }

    [Display("Enable lfs")]
    public bool? EnableLfs { get; set; }

    [Display("Enable request access")]
    public bool? EnableRequestAccess { get; set; }

    [Display("Tags")]
    public List<string>? Tags { get; set; }

    [Display("Enable printing merge request link")]
    public bool? EnablePrintingMergeRequestLink { get; set; }

    [Display("Ci config path")]
    public string? CiConfigPath { get; set; }
    
    [Display("Initialize with README")]
    public bool? InitializeWithReadme { get; set; }

    public CreateRepositoryRequest GetNewRepositoryRequest()
    {
       return new()
        {
            Name = Name,
            UserId = UserId is null ? null : int.Parse(UserId),
            DefaultBranch = DefaultBranch,
            NamespaceId = NamespaceId,
            Description = Description,
            EnableIssues = EnableIssues,
            EnableMergeRequests = EnableMergeRequests,
            EnableJobs = EnableJobs,
            EnableWiki = EnableWiki,
            EnableSnippets = EnableSnippets,
            EnableContainerRegistry = EnableContainerRegistry,
            EnableSharedRunners = EnableSharedRunners,
            Visibility = Visibility != null ? (ProjectVisibilityLevel)int.Parse(Visibility) : null,
            ImportUrl = ImportUrl,
            PublicJobs = PublicJobs,
            OnlyAllowMergeIfPipelineSucceeds = OnlyAllowMergeIfPipelineSucceeds,
            OnlyAllowMergeIfAllDiscussionsAreResolved = OnlyAllowMergeIfAllDiscussionsAreResolved,
            EnableLfs = EnableLfs,
            EnableRequestAccess = EnableRequestAccess,
            Tags = Tags,
            EnablePrintingMergeRequestLink = EnablePrintingMergeRequestLink,
            CiConfigPath = CiConfigPath,
            InitializeWithReadme = InitializeWithReadme
        };
    }
}