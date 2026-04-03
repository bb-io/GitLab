using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using Apps.GitLab.Utils;
using GitLabApiClient.Models.Trees.Responses;
using RestSharp;
using File = Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems.File;

namespace Apps.Gitlab.DataSourceHandlers;

public class FilePickerDataHandler : BaseInvocable, IAsyncFileDataSourceItemHandler
{
    private IEnumerable<AuthenticationCredentialsProvider> Creds =>
        InvocationContext.AuthenticationCredentialsProviders;

    private readonly GetRepositoryRequest _repositoryRequest;
    private readonly GetOptionalBranchRequest _branchRequest;

    public FilePickerDataHandler(
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
        var client = new BlackbirdGitlabClient(Creds);
        var folderPath = GitLabPathHelper.NormalizeFolderId(context?.FolderId);
        var request = client.CreateRequest($"/projects/{projectId}/repository/tree", Method.Get);
        request.AddQueryParameter("path", string.IsNullOrEmpty(folderPath) ? "/" : folderPath);

        if (!string.IsNullOrWhiteSpace(_branchRequest.Name))
            request.AddQueryParameter("ref", _branchRequest.Name);

        var tree = await client.ExecuteWithErrorHandling<List<Tree>>(request);

        return tree
            .OrderBy(x => x.Type == "blob")
            .ThenBy(x => GetDisplayName(x.Path), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Type == "tree"
                ? (FileDataItem)new Folder
                {
                    Id = GitLabPathHelper.NormalizePath(x.Path),
                    DisplayName = GetDisplayName(x.Path),
                    IsSelectable = false
                }
                : new File
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

        var directoryPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directoryPath))
            return Task.FromResult<IEnumerable<FolderPathItem>>(path);

        var currentPath = string.Empty;
        foreach (var part in directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
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

    private int GetProjectId()
    {
        if (string.IsNullOrWhiteSpace(_repositoryRequest.RepositoryId))
            throw new PluginMisconfigurationException("You should select a repository first.");

        return ParsingUtils.ParseIntOrThrow(_repositoryRequest.RepositoryId, "Repository ID");
    }

    private static string GetDisplayName(string path)
    {
        var normalizedPath = GitLabPathHelper.NormalizePath(path);
        return normalizedPath.Split('/').LastOrDefault() ?? normalizedPath;
    }
}
