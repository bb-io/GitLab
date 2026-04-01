using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using GitLabApiClient.Internal.Paths;

namespace Apps.Gitlab.DataSourceHandlers;

public class GitLabFolderPickerDataHandler : BaseInvocable, IAsyncFileDataSourceItemHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    private readonly GetRepositoryRequest _repositoryRequest;
    private readonly GetOptionalBranchRequest _branchRequest;

    public GitLabFolderPickerDataHandler(
        InvocationContext invocationContext,
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest) : base(invocationContext)
    {
        _repositoryRequest = repositoryRequest;
        _branchRequest = branchRequest;
    }

    public async Task<IEnumerable<FileDataItem>> GetFolderContentAsync(
        FolderContentDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var projectId = GetProjectId();
        var client = new BlackbirdGitlabClient(Creds).Client;
        var folderPath = GitLabPathHelper.NormalizeFolderId(context?.FolderId);

        var tree = await client.Trees.GetAsync(projectId, options =>
        {
            options.Path = string.IsNullOrEmpty(folderPath) ? "/" : folderPath;
            options.Reference = _branchRequest.Name;
        });

        return tree
            .Where(x => x.Type == "tree")
            .OrderBy(x => GetDisplayName(x.Path), StringComparer.OrdinalIgnoreCase)
            .Select(x => (FileDataItem)new Folder
            {
                Id = GitLabPathHelper.NormalizePath(x.Path),
                DisplayName = GetDisplayName(x.Path),
                IsSelectable = true
            })
            .ToList();
    }

    public Task<IEnumerable<FolderPathItem>> GetFolderPathAsync(
        FolderPathDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var path = new List<FolderPathItem>
        {
            new() { Id = GitLabPathHelper.RootId, DisplayName = GitLabPathHelper.RootDisplayName }
        };

        var normalizedPath = GitLabPathHelper.NormalizePath(context?.FileDataItemId);
        if (string.IsNullOrEmpty(normalizedPath))
            return Task.FromResult<IEnumerable<FolderPathItem>>(path);

        var currentPath = string.Empty;
        foreach (var part in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
            path.Add(new FolderPathItem
            {
                Id = currentPath,
                DisplayName = part
            });
        }

        return Task.FromResult<IEnumerable<FolderPathItem>>(path);
    }

    private ProjectId GetProjectId()
    {
        if (string.IsNullOrWhiteSpace(_repositoryRequest.RepositoryId))
            throw new PluginMisconfigurationException("You should select a repository first.");

        return (ProjectId)int.Parse(_repositoryRequest.RepositoryId);
    }

    private static string GetDisplayName(string path)
    {
        var normalizedPath = GitLabPathHelper.NormalizePath(path);
        return normalizedPath.Split('/').LastOrDefault() ?? normalizedPath;
    }
}
