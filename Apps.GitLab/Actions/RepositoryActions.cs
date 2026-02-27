using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Constants;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Commit.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Models.Respository.Responses;
using Apps.GitLab;
using Apps.GitLab.Models.Respository.Requests;
using Apps.GitLab.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Blackbird.Applications.Sdk.Utils.Models;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.Projects.Responses;
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
        var endpoint = "/api/v4/projects";

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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var repository = await ErrorHandler.ExecuteWithErrorHandlingAsync(() => Client.Projects.GetAsync(projectId));

        var branch = branchRequest.Name ?? repository.DefaultBranch;

        var fileData = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
            Client.Files.GetAsync(projectId, getFileRequest.FilePath, branch));
        if (fileData == null)
            throw new ArgumentException($"File does not exist ({getFileRequest.FilePath})");

        var filename = Path.GetFileName(getFileRequest.FilePath);
        if (!MimeTypes.TryGetMimeType(filename, out var mimeType))
            mimeType = MediaTypeNames.Application.Octet;

        FileReference file;
        File.WriteAllBytes("test", Convert.FromBase64String(fileData.Content));
        using (var stream = new MemoryStream(Convert.FromBase64String(fileData.Content)))
        {
            file = _fileManagementClient.UploadAsync(stream, mimeType, filename).Result;
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var resultFiles = new List<GitLabFile>();
        var content = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        RestClient.GetArchive(projectId, branchRequest.Name));
        if (content == null || content.Length == 0)
        {
            throw new PluginApplicationException("Repository is empty!");
        }

        var filesFromZip = new List<BlackbirdZipEntry>();
        using (var stream = new MemoryStream(content))
        {
            filesFromZip = (await stream.GetFilesFromZip()).ToList();
        }
        var includeSubFolders = folderContentRequest.IncludeSubfolders.HasValue && folderContentRequest.IncludeSubfolders.Value;
        foreach (var file in filesFromZip)
        {
            file.Path = file.Path.Substring(file.Path.IndexOf('/') + 1);
            if (file.FileStream.Length == 0)
            {
                continue;
            }
            else if (!string.IsNullOrEmpty(folderContentRequest.Path))
            {
                if ((includeSubFolders && !file.Path.StartsWith(folderContentRequest.Path)) ||
                    (!includeSubFolders && Path.GetDirectoryName(file.Path).TrimStart('\\').Replace('\\', '/') != folderContentRequest.Path.Trim('/')))
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
            resultFiles.Add(new GitLabFile()
            {
                File = uploadedFile,
                FilePath = file.Path
            });
        }
        return new GetRepositoryFilesFromFilepathsResponse { Files = resultFiles };
    }

    [Action("Get repository", Description = "Get repository info")]
    public async Task<Project> GetRepositoryById([ActionParameter] GetRepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        var repository = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.Projects.GetAsync(projectId));
        return repository;
    }

    [Action("Get repository issues", Description = "Get opened issues against repository")]
    public async Task<GetIssuesResponse> GetIssuesInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        var issues = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
       Client.Issues.GetAllAsync(projectId));
        return new()
        {
            Issues = issues.Select(issue => new IssueDto(issue))
        };
    }

    [Action("Get repository merge requests", Description = "Get opened merge requests in a repository")]
    public async Task<GetPullRequestsResponse> GetPullRequestsInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        var pullRequests = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.MergeRequests.GetAsync(projectId, _ => { }));
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var tree = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
         Client.Trees.GetAsync(projectId, options =>
         {
             options.Recursive = input.IncludeSubfolders ?? false;
             options.Path = input.Path ?? "/";
             if (!string.IsNullOrWhiteSpace(branchRequest.Name))
                 options.Reference = branchRequest.Name;
         }));
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
        var projects = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.Projects.GetAsync(options => { options.IsMemberOf = true; }));
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var branches = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
        Client.Branches.GetAsync(projectId, options => { options.Search = branchNameRequest; }));

        return branches.Any(x => x.Name == branchNameRequest);
    }
}