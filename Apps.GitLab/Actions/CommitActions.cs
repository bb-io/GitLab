using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Extensions;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Commit.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Webhooks;
using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.GitLab.Models.Commit.Requests;
using Apps.GitLab.Models.Commit.Responses;
using Apps.GitLab.Utils.File;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Filters.Constants;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Transformations;
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
        var fileStream = await fileManagementClient.DownloadAsync(input.File);
        var transformationResult = Transformation.Load(fileStream, input.File.Name, input.File.ContentType);

        string? content = null;
        var contentResult = transformationResult.Target();
        if (contentResult.Success)
        {
            content = contentResult.Value.ToStream(MetadataHandling.Exclude).ReadString();
        }
        else
        {
            InvocationContext.Logger?.LogInformation($"Not a Blackbird interoperable file: {transformationResult.Error}", []);
            content = fileStream.ReadString();
        }

        var pushResult = await RestClient.PushChanges(
            projectId, 
            branch, 
            input.CommitMessage,
            input.DestinationFilePath,
            System.Text.Encoding.UTF8.GetBytes(content), 
            action);
        
        var commitDto = new CommitDto(pushResult);

        if (!transformationResult.Success)
            return new(commitDto, input.File);

        var (blobUrl, editUrl) = TransformationExtensions.BuildUrls(input.DestinationFilePath, branch, RestClient.BaseUrl);

        var transformation = transformationResult.Value;
        transformation.TargetSystemReference.ContentName = input.File.Name;
        transformation.TargetSystemReference.AdminUrl = editUrl;
        transformation.TargetSystemReference.SystemName = "Gitlab";
        transformation.TargetSystemReference.SystemRef = "https://gitlab.com/";

        if (transformationResult.WasBilingual)
        {
            var transformedFile = await fileManagementClient.UploadAsync(
                transformation.ToStream(),
                MediaTypes.Xliff2,
                transformation.BilingualFileName);

            return new(commitDto, transformedFile);
        }

        var targetResult = transformation.Target();
        if (!targetResult.Success) throw new PluginMisconfigurationException(targetResult.Error);

        var target = targetResult.Value;
        target.SystemReference = transformation.TargetSystemReference;

        var targetFile = await fileManagementClient.UploadAsync(
            target.ToStream(),
            target.OriginalMediaType,
            target.OriginalName);

        return new(commitDto, targetFile);

    }
    
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
