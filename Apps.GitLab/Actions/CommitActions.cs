using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Commit.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Webhooks;
using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.GitLab.Models.Commit.Requests;
using Apps.GitLab.Models.Commit.Responses;
using Apps.GitLab.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Models.Commits.Responses;
using GitLabApiClient.Models.Projects.Responses;
using RestSharp;
using System.Net;

namespace Apps.Gitlab.Actions;

[ActionList("Commit")]
public class CommitActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
    : GitLabActions(invocationContext)
{
    private const int CommitsPageSize = 100;

    [Action("Search commits", Description = "Search commits in a repository")]
    public async Task<ListRepositoryCommitsResponse> ListRepositoryCommits(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] ListCommitsRequest searchRequest)
    {
        var commits = await SearchRepositoryCommits(repositoryRequest, branchRequest, searchRequest);

        return new()
        {
            Count = commits.Count,
            Commits = commits.Select(commit => new CommitResponse(commit))
        };
    }

    [Action("Find commit", Description = "Find first commit that matches search filters in a repository")]
    public async Task<CommitResponse> FindCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] SearchCommitsRequest searchRequest)
    {
        var commit = await FindRepositoryCommit(repositoryRequest, branchRequest, searchRequest)
            ?? throw new PluginApplicationException("No matching commit was found.");

        return new(commit);
    }

    private async Task<List<Commit>> SearchRepositoryCommits(
        GetRepositoryRequest repositoryRequest,
        GetOptionalBranchRequest branchRequest,
        ListCommitsRequest searchRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var includedAuthors = NormalizeFilterValues(searchRequest.AuthorsToInclude).ToList();
        var maximumResults = GetMaximumResults(searchRequest);
        var commits = new List<Commit>();
        var page = 1;

        while (true)
        {
            var pageCommits = await GetRepositoryCommitsPage(projectId, branchRequest, searchRequest, includedAuthors, page);
            if (pageCommits.Count == 0)
                break;

            var matchingCommits = FilterCommits(pageCommits, searchRequest, includedAuthors)
                .Take(maximumResults - commits.Count)
                .ToList();

            commits.AddRange(matchingCommits);
            if (commits.Count >= maximumResults)
                break;

            if (pageCommits.Count < CommitsPageSize)
                break;

            page++;
        }

        return commits;
    }

    private async Task<Commit?> FindRepositoryCommit(
        GetRepositoryRequest repositoryRequest,
        GetOptionalBranchRequest branchRequest,
        SearchCommitsRequest searchRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var includedAuthors = NormalizeFilterValues(searchRequest.AuthorsToInclude).ToList();
        var page = 1;

        while (true)
        {
            var pageCommits = await GetRepositoryCommitsPage(projectId, branchRequest, searchRequest, includedAuthors, page);
            if (pageCommits.Count == 0)
                return null;

            var commit = FilterCommits(pageCommits, searchRequest, includedAuthors).FirstOrDefault();
            if (commit is not null)
                return commit;

            if (pageCommits.Count < CommitsPageSize)
                return null;

            page++;
        }
    }

    private async Task<List<Commit>> GetRepositoryCommitsPage(
        int projectId,
        GetOptionalBranchRequest branchRequest,
        SearchCommitsRequest searchRequest,
        IReadOnlyCollection<string> includedAuthors,
        int page)
    {
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/commits", Method.Get);
        request.AddQueryParameter("per_page", CommitsPageSize.ToString());
        request.AddQueryParameter("page", page.ToString());

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref_name", branchRequest.Name);

        if (searchRequest.CommitAfter.HasValue)
            request.AddQueryParameter("since", FormatGitLabDate(searchRequest.CommitAfter.Value));

        if (searchRequest.CommitBefore.HasValue)
            request.AddQueryParameter("until", FormatGitLabDate(searchRequest.CommitBefore.Value));

        if (!string.IsNullOrWhiteSpace(searchRequest.FilePath))
            request.AddQueryParameter("path", searchRequest.FilePath);

        if (includedAuthors.Count == 1)
            request.AddQueryParameter("author", includedAuthors.First());

        return await RestClient.ExecuteWithErrorHandling<List<Commit>>(request);
    }

    [Action("Get commit", Description = "Get commit details by commit ID")]
    public async Task<CommitResponse> GetCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetCommitRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/repository/commits/{Uri.EscapeDataString(input.CommitId)}",
            Method.Get);

        return new(await RestClient.ExecuteWithErrorHandling<Commit>(request));
    }

    [Action("Search added or modified files in X hours", Description = "Search files added or modified during specified number of hours")]
    public async Task<ListAddedOrModifiedInHoursResponse> ListAddedOrModifiedInHours(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] AddedOrModifiedHoursRequest hoursRequest,
        [ActionParameter] FolderRequest folderInput)
    {
        if (hoursRequest.Hours <= 0)
            throw new ArgumentException("Specify more than 0 hours!");

        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/commits", Method.Get);
        request.AddQueryParameter("since", DateTime.Now.AddHours(-hoursRequest.Hours).ToString("O"));

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref_name", branchRequest.Name);

        var commits = await RestClient.ExecuteWithErrorHandling<List<Commit>>(request);
        var files = new List<AddedOrModifiedFile>();

        foreach (var commit in commits)
        {
            var diffRequest = RestClient.CreateRequest(
                $"/projects/{projectId}/repository/commits/{Uri.EscapeDataString(commit.Id)}/diff",
                Method.Get);
            var diffs = await RestClient.ExecuteWithErrorHandling<List<Diff>>(diffRequest);

            files.AddRange(
                diffs
                    .Where(x => !x.IsDeletedFile)
                    .Where(f =>
                        string.IsNullOrEmpty(folderInput.FolderPath) ||
                        PushWebhooks.IsFilePathMatchingPattern(folderInput.FolderPath, f.NewPath))
                    .Select(x => new AddedOrModifiedFile(x)));
        }

        return new ListAddedOrModifiedInHoursResponse
        {
            Files = files.DistinctBy(x => x.Filename).ToList()
        };
    }

    [Action("Upload file", Description = "Create file or update existing file in a repository")]
    public async Task<UploadFileResponse> PushFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;

        var action = await CheckFileExists(projectId, input.DestinationFilePath, branch)
            ? GitLabCommitActions.Update
            : GitLabCommitActions.Create;
        
        return await CommitFile(repository, branch, input, action);
    }

    [Action("Upload multiple files", Description = "Create or update multiple files in a repository in one commit")]
    public async Task<UploadFilesResponse> PushFiles(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFilesRequest input)
    {
        var files = input.Files?.ToList() ?? [];
        var destinationFilePaths = input.DestinationFilePaths?
            .Select(path => path?.Trim().Trim('/') ?? string.Empty)
            .ToList() ?? [];
        ValidateMultipleFileInput(files, destinationFilePaths);

        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;
        var preparedFiles = new List<PreparedFileUpload>(files.Count);

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var destinationFilePath = destinationFilePaths[index];
            var action = await CheckFileExists(projectId, destinationFilePath, branch)
                ? GitLabCommitActions.Update
                : GitLabCommitActions.Create;

            preparedFiles.Add(await PrepareFileUpload(file, destinationFilePath, action));
        }

        var pushResult = await RestClient.PushChanges(
            projectId,
            branch,
            input.CommitMessage,
            preparedFiles.Select(file => new FileActionDto(
                file.Action,
                file.DestinationFilePath,
                file.Content)));

        var uploadedFiles = new List<UploadedFileResponse>(preparedFiles.Count);
        foreach (var preparedFile in preparedFiles)
            uploadedFiles.Add(await CreateUploadedFileResponse(repository, branch, pushResult, preparedFile));

        return new UploadFilesResponse(new CommitDto(pushResult), uploadedFiles);
    }

    [Action("Update file", Description = "Update existing file in a repository")]
    public async Task<UploadFileResponse> UpdateFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var repository = await RestClient.GetProject(projectId);
        var branch = branchRequest.Name ?? repository.DefaultBranch;
        
        var fileExists = await CheckFileExists(projectId, input.DestinationFilePath, branch);
        if (!fileExists)
            throw new PluginMisconfigurationException("File does not exist");
        
        return await CommitFile(repository, branch, input, GitLabCommitActions.Update);
    }
    
    private async Task<UploadFileResponse> CommitFile(
        Project repository, 
        string branch,
        PushFileRequest input,
        string action)
    {
        int projectId = repository.Id;
        await using var downloadedFileStream = await fileManagementClient.DownloadAsync(input.File);
        using var fileStream = new MemoryStream();
        await downloadedFileStream.CopyToAsync(fileStream);
        fileStream.Position = 0;

        var processedFile = await InteroperableFileHelper.StripMetadata(
            fileStream: fileStream,
            fileName: input.File.Name,
            contentType: input.File.ContentType,
            logger: InvocationContext.Logger);

        var pushResult = await RestClient.PushChanges(
            projectId, 
            branch, 
            input.CommitMessage,
            input.DestinationFilePath,
            processedFile.Content,
            action);
        
        var commitDto = new CommitDto(pushResult);

        if (processedFile.MetadataType is null)
            return new(commitDto, input.File, 0, null);

        fileStream.Position = 0;
        var metadataFile = InteroperableFileHelper.AddMetadata(
            fileStream: fileStream,
            fileName: input.File.Name,
            contentType: input.File.ContentType,
            path: input.DestinationFilePath,
            branchName: branch,
            repoWebUrl: repository.WebUrl,
            repoPathWithNamespace: repository.PathWithNamespace,
            baseUrl: RestClient.BaseUrl,
            dateChanged: new DateTimeOffset(pushResult.CommittedDate),
            reviewProvenance: null,
            metadataType: processedFile.MetadataType.Value,
            logger: InvocationContext.Logger);

        var targetFile = await fileManagementClient.UploadAsync(
            metadataFile.FileStream,
            metadataFile.MimeType,
            metadataFile.FileName);

        return new(commitDto, targetFile, metadataFile.NumberOfUnits, metadataFile.Metadata);
    }

    private async Task<PreparedFileUpload> PrepareFileUpload(
        FileReference file,
        string destinationFilePath,
        string action)
    {
        await using var downloadedFileStream = await fileManagementClient.DownloadAsync(file);
        using var fileStream = new MemoryStream();
        await downloadedFileStream.CopyToAsync(fileStream);
        var originalContent = fileStream.ToArray();
        fileStream.Position = 0;

        var processedFile = await InteroperableFileHelper.StripMetadata(
            fileStream: fileStream,
            fileName: file.Name,
            contentType: file.ContentType,
            logger: InvocationContext.Logger);

        return new PreparedFileUpload(
            file,
            destinationFilePath,
            action,
            originalContent,
            processedFile.Content,
            processedFile.MetadataType);
    }

    private async Task<UploadedFileResponse> CreateUploadedFileResponse(
        Project repository,
        string branch,
        Commit pushResult,
        PreparedFileUpload preparedFile)
    {
        if (preparedFile.MetadataType is null)
            return new UploadedFileResponse(preparedFile.File, preparedFile.DestinationFilePath, 0, null);

        using var originalFileStream = new MemoryStream(preparedFile.OriginalContent);
        var metadataFile = InteroperableFileHelper.AddMetadata(
            fileStream: originalFileStream,
            fileName: preparedFile.File.Name,
            contentType: preparedFile.File.ContentType,
            path: preparedFile.DestinationFilePath,
            branchName: branch,
            repoWebUrl: repository.WebUrl,
            repoPathWithNamespace: repository.PathWithNamespace,
            baseUrl: RestClient.BaseUrl,
            dateChanged: new DateTimeOffset(pushResult.CommittedDate),
            reviewProvenance: null,
            metadataType: preparedFile.MetadataType.Value,
            logger: InvocationContext.Logger);

        using (metadataFile.FileStream)
        {
            var targetFile = await fileManagementClient.UploadAsync(
                metadataFile.FileStream,
                metadataFile.MimeType,
                metadataFile.FileName);

            return new UploadedFileResponse(
                targetFile,
                preparedFile.DestinationFilePath,
                metadataFile.NumberOfUnits,
                metadataFile.Metadata);
        }
    }

    private static void ValidateMultipleFileInput(
        IReadOnlyCollection<FileReference> files,
        IReadOnlyCollection<string> destinationFilePaths)
    {
        if (files.Count == 0)
            throw new PluginMisconfigurationException("At least one file must be provided.");

        if (files.Count != destinationFilePaths.Count)
            throw new PluginMisconfigurationException(
                "The number of files must match the number of destination file paths.");

        if (destinationFilePaths.Any(string.IsNullOrWhiteSpace))
            throw new PluginMisconfigurationException("Destination file paths cannot be empty.");

        if (destinationFilePaths.Distinct(StringComparer.Ordinal).Count() != destinationFilePaths.Count)
            throw new PluginMisconfigurationException("Destination file paths must be unique.");
    }

    private sealed record PreparedFileUpload(
        FileReference File,
        string DestinationFilePath,
        string Action,
        byte[] OriginalContent,
        byte[] Content,
        BlackbirdMetadataType? MetadataType);
    
    public async Task<bool> CheckFileExists(int projectId, string filePath, string branch)
    {
        var endpoint = $"/projects/{projectId}/repository/files/{Uri.EscapeDataString(filePath.Trim('/'))}";
        var request = RestClient.CreateRequest(endpoint, Method.Head).AddQueryParameter("ref", branch);
        
        var response = await RestClient.ExecuteWithErrorHandling(request, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode;
    }

    [Action("Delete file", Description = "Delete file from a repository")]
    public async Task<DeleteFileResponse> DeleteFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] DeleteFileRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var fileDelete = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
            input.FilePath, null, GitLabCommitActions.Delete);

        return new()
        {
            CommitId = fileDelete.Id,
            Title = fileDelete.Title,
            Message = fileDelete.Message
        };
    }

    private static IEnumerable<Commit> FilterCommits(
        IEnumerable<Commit> commits,
        SearchCommitsRequest searchRequest,
        IReadOnlyCollection<string> includedAuthors)
    {
        var excludedAuthors = NormalizeFilterValues(searchRequest.AuthorsToExclude).ToList();
        var messageFilter = searchRequest.CommitMessageContains?.Trim();

        return commits
            .Where(commit => !searchRequest.CommitAfter.HasValue ||
                             commit.CreatedAt.ToUniversalTime() > searchRequest.CommitAfter.Value.ToUniversalTime())
            .Where(commit => !searchRequest.CommitBefore.HasValue ||
                             commit.CreatedAt.ToUniversalTime() < searchRequest.CommitBefore.Value.ToUniversalTime())
            .Where(commit => includedAuthors.Count == 0 || AuthorMatches(commit, includedAuthors))
            .Where(commit => excludedAuthors.Count == 0 || !AuthorMatches(commit, excludedAuthors))
            .Where(commit => string.IsNullOrWhiteSpace(messageFilter) || CommitMessageMatches(commit, messageFilter));
    }

    private static IEnumerable<string> NormalizeFilterValues(IEnumerable<string>? values)
        => values?
               .Where(value => !string.IsNullOrWhiteSpace(value))
               .Select(value => value.Trim())
           ?? Enumerable.Empty<string>();

    private static int GetMaximumResults(ListCommitsRequest searchRequest)
    {
        var maximumResults = searchRequest.MaximumResults ?? 100;
        if (maximumResults <= 0)
            throw new PluginMisconfigurationException("Maximum results must be greater than 0.");

        return maximumResults;
    }

    private static bool AuthorMatches(Commit commit, IEnumerable<string> authors)
        => authors.Any(author =>
            ContainsIgnoreCase(commit.AuthorName, author) ||
            ContainsIgnoreCase(commit.AuthorEmail, author));

    private static bool CommitMessageMatches(Commit commit, string messageFilter)
        => ContainsIgnoreCase(commit.Message, messageFilter) ||
           ContainsIgnoreCase(commit.Title, messageFilter);

    private static bool ContainsIgnoreCase(string? value, string searchValue)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(searchValue, StringComparison.OrdinalIgnoreCase);

    private static string FormatGitLabDate(DateTime date)
        => date.ToUniversalTime().ToString("O");
}
