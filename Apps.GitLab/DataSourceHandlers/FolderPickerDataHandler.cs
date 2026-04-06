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

namespace Apps.Gitlab.DataSourceHandlers;

public class FolderPickerDataHandler(
    InvocationContext invocationContext,
    [ActionParameter] GetRepositoryRequest repositoryRequest,
    [ActionParameter] GetOptionalBranchRequest branchRequest)
    : GitLabDataHandler(invocationContext), IAsyncFileDataSourceItemHandler
{

    public async Task<IEnumerable<FileDataItem>> GetFolderContentAsync(
        FolderContentDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var projectId = GetProjectId(repositoryRequest.RepositoryId);
        var folderPath = GitLabPathHelper.NormalizeFolderId(context?.FolderId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/tree", Method.Get);
        request.AddQueryParameter("path", string.IsNullOrEmpty(folderPath) ? "/" : folderPath);

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref", branchRequest.Name);

        var tree = await RestClient.ExecuteWithErrorHandling<List<Tree>>(request);

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

}
