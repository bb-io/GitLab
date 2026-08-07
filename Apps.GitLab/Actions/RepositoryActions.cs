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
using Apps.GitLab.Utils;
using GitLabApiClient.Models.Commits.Responses;
using Blackbird.Filters.Shared;

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
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;

        return await GetFile(projectId, repository, branch, getFileRequest.FilePath);
    }

    private async Task<GetFileResponse> GetFile(
        int projectId,
        Project repository,
        string branch,
        string filePath)
    {
        var fileInfo = await RestClient.GetFileInfo(projectId, filePath, branch);
        var latestCommit = await GetLatestFileCommit(projectId, filePath, branch);

        var fileName = Path.GetFileName(filePath);
        var mimeType = MimeTypes.GetMimeType(fileName);
        var fileStream = new MemoryStream(Convert.FromBase64String(fileInfo.Content));
        var fileWithMetadata = InteroperableFileHelper.AddMetadata(
            fileStream: fileStream,
            fileName: fileName,
            contentType: mimeType,
            path: filePath,
            repoWebUrl: repository.WebUrl,
            branchName: branch,
            repoPathWithNamespace: repository.PathWithNamespace,
            baseUrl: RestClient.BaseUrl,
            dateChanged: new DateTimeOffset(latestCommit.CommittedDate),
            reviewProvenance: CreateReviewProvenance(latestCommit),
            metadataType: BlackbirdMetadataType.Source,
            logger: InvocationContext.Logger);
        
        var fileReference = await fileManagementClient.UploadAsync(
            fileWithMetadata.FileStream,
            fileWithMetadata.MimeType,
            fileWithMetadata.FileName);

        return new GetFileResponse
        {
            File = fileReference,
            FilePath = filePath,
            FileExtension = Path.GetExtension(filePath),
            NumberOfUnits = fileWithMetadata.NumberOfUnits,
            Metadata = fileWithMetadata.Metadata
        };
    }

    [Action("Get all files in folder", Description = "Get files from a repository folder")]
    public async Task<GetRepositoryFilesFromFilepathsResponse> GetAllFilesInFolder(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] FolderContentRequest folderContentRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;
        var resultFiles = new List<GitLabFile>();
        var metadata = new List<Apps.GitLab.Models.Responses.MetadataResponse>();
        var numberOfUnits = 0;
        var content = await RestClient.GetArchive(projectId, branch);
        if (content.Length == 0)
            throw new PluginMisconfigurationException("Repository is empty!");

        List<BlackbirdZipEntry> filesFromZip;
        using (var stream = new MemoryStream(content))
        {
            filesFromZip = (await stream.GetFilesFromZip()).ToList();
        }

        var includeSubFolders = folderContentRequest.IncludeSubfolders.GetValueOrDefault();
        var normalizedFolderPath = folderContentRequest.Path?.Trim('/');
        var selectedFiles = new List<BlackbirdZipEntry>();
        foreach (var file in filesFromZip)
        {
            file.Path = file.Path.Substring(file.Path.IndexOf('/') + 1);
            if (file.FileStream.Length == 0)
                continue;

            if (!string.IsNullOrEmpty(normalizedFolderPath))
            {
                var normalizedDirectory = Path.GetDirectoryName(file.Path)?.TrimStart('\\').Replace('\\', '/');

                var shouldBeSkipped = (includeSubFolders && !file.Path.StartsWith(normalizedFolderPath))
                    || (!includeSubFolders && normalizedDirectory != normalizedFolderPath);

                if (shouldBeSkipped)
                    continue;
            }
            
            if (string.IsNullOrEmpty(normalizedFolderPath) &&
                !includeSubFolders &&
                !string.IsNullOrEmpty(Path.GetDirectoryName(file.Path)))
                continue;

            selectedFiles.Add(file);
        }

        foreach (var file in selectedFiles)
        {
            using var fileStream = new MemoryStream();
            await file.FileStream.CopyToAsync(fileStream);
            var latestCommit = await GetLatestFileCommit(projectId, file.Path, branch);

            var fileName = Path.GetFileName(file.Path);
            var mimeType = MimeTypes.GetMimeType(fileName);
            fileStream.Position = 0;
            var fileWithMetadata = InteroperableFileHelper.AddMetadata(
                fileStream: fileStream,
                fileName: fileName,
                contentType: mimeType,
                path: file.Path,
                repoWebUrl: repository.WebUrl,
                branchName: branch,
                repoPathWithNamespace: repository.PathWithNamespace,
                baseUrl: RestClient.BaseUrl,
                dateChanged: new DateTimeOffset(latestCommit.CommittedDate),
                reviewProvenance: CreateReviewProvenance(latestCommit),
                metadataType: BlackbirdMetadataType.Source,
                logger: InvocationContext.Logger);

            var uploadedFile = await fileManagementClient.UploadAsync(
                fileWithMetadata.FileStream,
                fileWithMetadata.MimeType,
                fileWithMetadata.FileName);

            resultFiles.Add(new GitLabFile { File = uploadedFile, FilePath = file.Path });
            numberOfUnits += fileWithMetadata.NumberOfUnits;
            if (fileWithMetadata.Metadata is not null)
                metadata.Add(fileWithMetadata.Metadata);
        }

        return new GetRepositoryFilesFromFilepathsResponse
        {
            Files = resultFiles,
            NumberOfUnits = numberOfUnits,
            Metadata = metadata
        };
    }

    [Action("Get repository", Description = "Get repository details")]
    public async Task<RepositoryResponse> GetRepositoryById([ActionParameter] GetRepositoryRequest input)
    {
        var project = await RestClient.GetProject(ParseProjectId(input.RepositoryId));
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
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;
        var files = new List<GitLabFile>();
        var metadata = new List<Apps.GitLab.Models.Responses.MetadataResponse>();
        var numberOfUnits = 0;
        foreach (var filePath in input.FilePaths)
        {
            var fileData = await GetFile(projectId, repository, branch, filePath);

            files.Add(new GitLabFile
            {
                FilePath = fileData.FilePath,
                File = fileData.File
            });
            numberOfUnits += fileData.NumberOfUnits;

            if (fileData.Metadata is not null)
                metadata.Add(fileData.Metadata);
        }

        return new()
        {
            Files = files,
            NumberOfUnits = numberOfUnits,
            Metadata = metadata
        };
    }

    private async Task<Commit> GetLatestFileCommit(int projectId, string filePath, string branch)
    {
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/commits", Method.Get);
        request.AddQueryParameter("ref_name", branch);
        request.AddQueryParameter("path", filePath);
        request.AddQueryParameter("per_page", "1");

        var commits = await RestClient.ExecuteWithErrorHandling<List<Commit>>(request);
        return commits.FirstOrDefault()
            ?? throw new PluginApplicationException($"No commit was found for file '{filePath}' on branch '{branch}'.");
    }

    private static ProvenanceRecord CreateReviewProvenance(Commit commit)
        => new()
        {
            Person = commit.AuthorName,
            PersonReference = commit.AuthorEmail,
            Tool = "GitLab",
            ToolReference = commit.WebUrl
        };

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
}
