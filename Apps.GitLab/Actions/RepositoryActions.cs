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
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Blackbird.Applications.Sdk.Utils.Models;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Models.Projects.Responses;
using GitLabApiClient.Models.Trees.Responses;
using RestSharp;
using System.Net.Mime;
using Apps.GitLab.Utils.File;

namespace Apps.Gitlab.Actions;

[ActionList("Repository")]
public class RepositoryActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : GitLabActions(invocationContext)
{
    [Action("Create new repository", Description = "Create repository with selected settings")]
    public async Task<RepositoryResponse> CreateRepository([ActionParameter] CreateRepositoryInput input)
    {
        var endpoint = "/projects";

        if (input.UserId != null)
            endpoint += $"/user/{input.UserId}";

        var request = RestClient.CreateRequest(endpoint, Method.Post)
            .WithJsonBody(input.GetNewRepositoryRequest(), JsonConfig.JsonSettings);

        var project = await RestClient.ExecuteWithErrorHandling<Project>(request);
        return RepositoryResponse.FromProject(project);
    }

    [Action("Download file", Description = "Download a file from a repository by file path")]
    public async Task<GetFileResponse> GetFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] GetFileRequest getFileRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;

        string endpoint = $"/projects/{projectId}/repository/files/{Uri.EscapeDataString(getFileRequest.FilePath)}";
        var request = RestClient.CreateRequest(endpoint, Method.Get).AddQueryParameter("ref", branch);

        var repoFileResponse = await RestClient.ExecuteWithErrorHandling<RepositoryFileResponse>(request) ?? 
                               throw new PluginMisconfigurationException($"File does not exist ({getFileRequest.FilePath})");

        var fileToProcess = new FileToProcess(repoFileResponse.Content, getFileRequest.FilePath, repository.WebUrl, branch);
        var fileData = FileHelper.ProcessDownloadedFile(fileToProcess);
        var fileReference = await fileManagementClient.UploadAsync(fileData.FileStream, fileData.MimeType, fileData.FileName);
        return new GetFileResponse
        {
            FilePath = getFileRequest.FilePath,
            File = fileReference,
            FileExtension = Path.GetExtension(getFileRequest.FilePath)
        };
    }

    [Action("Get all files in folder", Description = "Get files from a repository folder")]
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

            var uploadedFile = await fileManagementClient.UploadAsync(file.FileStream, mimeType, filename);
            resultFiles.Add(new GitLabFile
            {
                File = uploadedFile,
                FilePath = file.Path
            });
        }

        return new GetRepositoryFilesFromFilepathsResponse { Files = resultFiles };
    }

    [Action("Get repository", Description = "Get repository details")]
    public async Task<RepositoryResponse> GetRepositoryById([ActionParameter] GetRepositoryRequest input)
    {
        var project = await GetProject(ParseProjectId(input.RepositoryId));
        return RepositoryResponse.FromProject(project);
    }

    [Action("Search repository issues", Description = "Get open issues in a repository")]
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

    [Action("Search repository merge requests", Description = "Get open merge requests in a repository")]
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

    [Action("Search repository folder content", Description = "Search folder content in a repository")]
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

    [Action("Search repositories", Description = "Search repositories available to connection")]
    public async Task<ListRepositoriesResponse> ListRepositories()
    {
        var request = RestClient.CreateRequest("/projects", Method.Get);
        request.AddQueryParameter("membership", "true");

        var projects = await RestClient.ExecuteWithErrorHandling<List<Project>>(request);
        return new(projects.ToArray());
    }

    [Action("Search files by filepaths", Description = "Get files from a repository by file paths")]
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

    [Action("Check if branch exists", Description = "Check whether branch exists in a repository")]
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
