using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Constants;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Commit.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Models.Respository.Responses;
using Apps.GitLab;
using Apps.GitLab.Models.Respository.Requests;
using Apps.GitLab.Models.Respository.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Blackbird.Applications.Sdk.Utils.Models;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Models.Projects.Responses;
using GitLabApiClient.Models.Trees.Responses;
using RestSharp;
using System.Net.Mime;

namespace Apps.Gitlab.Actions;

[ActionList("Repository")]
public class RepositoryActions : GitLabActions
{
    private readonly IFileManagementClient _fileManagementClient;

    public RepositoryActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Create new repository", Description = "Create new repository")]
    public Task<Project> CreateRepository([ActionParameter] CreateRepositoryInput input)
    {
        var endpoint = "/projects";

        if (input.UserId != null)
            endpoint += $"/user/{input.UserId}";

        var request = new GitLabRequest(endpoint, Method.Post, Creds)
            .WithJsonBody(input.GetNewRepositoryRequest(), JsonConfig.JsonSettings);

        return RestClient.ExecuteWithErrorHandling<Project>(request);
    }

    [Action("Get repository file", Description = "Get repository file by path")]
    public async Task<GetFileResponse> GetFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] GetFileRequest getFileRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;

        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/repository/files/{Uri.EscapeDataString(getFileRequest.FilePath)}",
            Method.Get);
        request.AddQueryParameter("ref", branch);

        var fileData = await RestClient.ExecuteWithErrorHandling<RepositoryFileResponse>(request);
        if (fileData == null)
            throw new PluginMisconfigurationException($"File does not exist ({getFileRequest.FilePath})");

        var filename = Path.GetFileName(getFileRequest.FilePath);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        FileReference file;
        using (var stream = new MemoryStream(Convert.FromBase64String(fileData.Content)))
        {
            file = await _fileManagementClient.UploadAsync(stream, mimeType, filename);
        }

        return new GetFileResponse
        {
            FilePath = getFileRequest.FilePath,
            File = file,
            FileExtension = Path.GetExtension(getFileRequest.FilePath)
        };
    }

    [Action("Get all files in folder", Description = "Get all files in folder")]
    public async Task<GetRepositoryFilesFromFilepathsResponse> GetAllFilesInFolder(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] FolderContentRequest folderContentRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var resultFiles = new List<GitLabFile>();
        var content = await RestClient.GetArchive(projectId, branchRequest.Name);
        if (content.Length == 0)
            throw new PluginMisconfigurationException("Repository is empty!");

        List<BlackbirdZipEntry> filesFromZip;
        using (var stream = new MemoryStream(content))
        {
            filesFromZip = (await stream.GetFilesFromZip()).ToList();
        }

        var includeSubFolders = folderContentRequest.IncludeSubfolders.GetValueOrDefault();
        foreach (var file in filesFromZip)
        {
            file.Path = file.Path.Substring(file.Path.IndexOf('/') + 1);
            if (file.FileStream.Length == 0)
                continue;

            if (!string.IsNullOrEmpty(folderContentRequest.Path))
            {
                var normalizedFolderPath = folderContentRequest.Path.Trim('/');
                var normalizedDirectory = Path.GetDirectoryName(file.Path)?.TrimStart('\\').Replace('\\', '/');

                if ((includeSubFolders && !file.Path.StartsWith(folderContentRequest.Path)) ||
                    (!includeSubFolders && normalizedDirectory != normalizedFolderPath))
                {
                    continue;
                }
            }
            else if (!includeSubFolders && !string.IsNullOrEmpty(Path.GetDirectoryName(file.Path)))
            {
                continue;
            }

            var filename = Path.GetFileName(file.Path);
            if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
                mimeType = MediaTypeNames.Application.Octet;

            var uploadedFile = await _fileManagementClient.UploadAsync(file.FileStream, mimeType, filename);
            resultFiles.Add(new GitLabFile
            {
                File = uploadedFile,
                FilePath = file.Path
            });
        }

        return new GetRepositoryFilesFromFilepathsResponse { Files = resultFiles };
    }

    [Action("Get repository", Description = "Get repository info")]
    public Task<Project> GetRepositoryById([ActionParameter] GetRepositoryRequest input)
        => GetProject(ParseProjectId(input.RepositoryId));

    [Action("Get repository issues", Description = "Get opened issues against repository")]
    public async Task<GetIssuesResponse> GetIssuesInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = ParseProjectId(input.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/issues", Method.Get);
        var issues = await RestClient.ExecuteWithErrorHandling<List<GitLabApiClient.Models.Issues.Responses.Issue>>(request);

        return new()
        {
            Issues = issues.Select(issue => new IssueDto(issue))
        };
    }

    [Action("Get repository merge requests", Description = "Get opened merge requests in a repository")]
    public async Task<GetPullRequestsResponse> GetPullRequestsInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = ParseProjectId(input.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/merge_requests", Method.Get);
        var pullRequests = await RestClient.ExecuteWithErrorHandling<List<GitLabApiClient.Models.MergeRequests.Responses.MergeRequest>>(request);

        return new()
        {
            PullRequests = pullRequests.Select(p => new PullRequestDto(p))
        };
    }

    [Action("List repository folder content", Description = "List repository folder content")]
    public async Task<RepositoryContentResponse> ListRepositoryContent(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] FolderContentWithTypeRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/tree", Method.Get);
        request.AddQueryParameter("recursive", (input.IncludeSubfolders ?? false).ToString().ToLowerInvariant());
        request.AddQueryParameter("path", input.Path ?? "/");

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref", branchRequest.Name);

        var tree = await RestClient.ExecuteWithErrorHandling<List<Tree>>(request);
        if (!string.IsNullOrEmpty(input.ContentType))
            tree = tree.Where(x => input.ContentType.Split(' ').Contains(x.Type)).ToList();

        return new()
        {
            Content = tree
        };
    }

    [Action("List repositories", Description = "List all repositories")]
    public async Task<ListRepositoriesResponse> ListRepositories()
    {
        var request = RestClient.CreateRequest("/projects", Method.Get);
        request.AddQueryParameter("membership", "true");

        var projects = await RestClient.ExecuteWithErrorHandling<List<Project>>(request);
        return new(projects.ToArray());
    }

    [Action("Get files by filepaths", Description = "Get files by filepaths from webhooks")]
    public async Task<GetRepositoryFilesFromFilepathsResponse> GetRepositoryFilesFromFilepaths(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] GetRepositoryFilesFromFilepathsRequest input)
    {
        var files = new List<GitLabFile>();
        foreach (var filePath in input.FilePaths)
        {
            var fileData = await GetFile(
                repositoryRequest,
                branchRequest,
                new GetFileRequest { FilePath = filePath });

            files.Add(new GitLabFile
            {
                FilePath = fileData.FilePath,
                File = fileData.File
            });
        }

        return new()
        {
            Files = files
        };
    }

    [Action("Branch exists", Description = "Branch exists in specified repository")]
    public async Task<bool> BranchExists(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter][Display("Branch name")] string branchNameRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/branches", Method.Get);
        request.AddQueryParameter("search", branchNameRequest);

        var branches = await RestClient.ExecuteWithErrorHandling<List<GitLabApiClient.Models.Branches.Responses.Branch>>(request);
        return branches.Any(x => x.Name == branchNameRequest);
    }

    private Task<Project> GetProject(int projectId)
    {
        var request = RestClient.CreateRequest($"/projects/{projectId}", Method.Get);
        return RestClient.ExecuteWithErrorHandling<Project>(request);
    }
}
