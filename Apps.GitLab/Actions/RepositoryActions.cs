using Blackbird.Applications.Sdk.Common;
using Apps.Gitlab.Dtos;
using Blackbird.Applications.Sdk.Common.Actions;
using Apps.Gitlab.Models.Respository.Responses;
using Apps.Gitlab.Models.Respository.Requests;
using System.Net.Mime;
using Apps.Gitlab.Actions.Base;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Apps.Gitlab.Models.Branch.Requests;
using GitLabApiClient.Models.Projects.Responses;
using GitLabApiClient.Internal.Paths;
using Apps.GitLab;
using Apps.Gitlab.Models.Commit.Responses;
using Blackbird.Applications.Sdk.Utils.Models;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Apps.GitLab.Dtos;
using GitLabApiClient;
using Apps.GitLab.Models.Respository.Requests;

namespace Apps.Gitlab.Actions;

[ActionList]
public class RepositoryActions : GitLabActions
{
    private readonly IFileManagementClient _fileManagementClient;
    
    public RepositoryActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) 
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Create new repository", Description = "Create new repository")]
    public async Task<Project> CreateRepository([ActionParameter] CreateRepositoryRequest input)
    {
        try {
            var repository = await Client.Projects.CreateAsync(input.GetNewRepositoryRequest());
            return repository;
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get repository file", Description = "Get repository file by path")]
    public async Task<GetFileResponse> GetFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] GetFileRequest getFileRequest)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var repository = await Client.Projects.GetAsync(projectId);
            var fileData = await Client.Files.GetAsync(projectId, getFileRequest.FilePath, branchRequest.Name ?? repository.DefaultBranch);
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
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get all files in folder", Description = "Get all files in folder")]
    public async Task<GetRepositoryFilesFromFilepathsResponse> GetAllFilesInFolder(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] FolderContentRequest folderContentRequest)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var resultFiles = new List<GitLabFile>();
            var content = await RestClient.GetArchive(projectId, branchRequest.Name);
            if (content == null || content.Length == 0)
            {
                throw new ArgumentException("Repository is empty!");
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
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get repository", Description = "Get repository info")]
    public async Task<Project> GetRepositoryById([ActionParameter] GetRepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        try {
            var repository = await Client.Projects.GetAsync(projectId);
            return repository;
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get repository issues", Description = "Get opened issues against repository")]
    public async Task<GetIssuesResponse> GetIssuesInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        try {
            var issues = await Client.Issues.GetAllAsync(projectId);
            return new()
            {
                Issues = issues.Select(issue => new IssueDto(issue))
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get repository merge requests", Description = "Get opened merge requests in a repository")]
    public async Task<GetPullRequestsResponse> GetPullRequestsInRepository([ActionParameter] RepositoryRequest input)
    {
        var projectId = (ProjectId)int.Parse(input.RepositoryId);
        try {
            var pullRequests = await Client.MergeRequests.GetAsync(projectId, (options) => {});
            return new()
            {
                PullRequests = pullRequests.Select(p => new PullRequestDto(p))
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("List repository folder content", Description = "List repository folder content")]
    public async Task<RepositoryContentResponse> ListRepositoryContent(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] FolderContentWithTypeRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var tree = await Client.Trees.GetAsync(projectId, (options) => 
            { 
                options.Recursive = input.IncludeSubfolders ?? false;
                options.Path = input.Path ?? "/";
                if (!string.IsNullOrWhiteSpace(branchRequest.Name))
                    options.Reference = branchRequest.Name;
            });
            if (!string.IsNullOrEmpty(input.ContentType))
                tree = tree.Where(x => input.ContentType.Split(' ').Contains(x.Type)).ToList();
            return new()
            {
                Content = tree
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("List repositories", Description = "List all repositories")]
    public async Task<ListRepositoriesResponse> ListRepositories()
    {
        try {
            var projects = await Client.Projects.GetAsync((options) => { options.IsMemberOf = true; });
            return new(projects.ToArray());
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get files by filepaths", Description = "Get files by filepaths from webhooks")]
    public async Task<GetRepositoryFilesFromFilepathsResponse> GetRepositoryFilesFromFilepaths(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] GetRepositoryFilesFromFilepathsRequest input)
    {
        try
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
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Branch exists", Description = "Branch exists in specified repository")]
    public async Task<bool> BranchExists(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter][Display("Branch name")] string branchNameRequest)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try {
            var branches = await Client.Branches.GetAsync(projectId, (options) => { options.Search = branchNameRequest; });
            return branches.Any(x => x.Name == branchNameRequest);
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }
}