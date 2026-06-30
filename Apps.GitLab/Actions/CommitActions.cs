using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Commit.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Webhooks;
using Apps.GitLab.Constants;
using Apps.GitLab.Dtos;
using Apps.GitLab.Models.Commit.Requests;
using Apps.GitLab.Models.Commit.Responses;
using Apps.GitLab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using GitLabApiClient.Models.Commits.Responses;
using RestSharp;

namespace Apps.Gitlab.Actions;

[ActionList("Commit")]
public class CommitActions : GitLabActions
{
    private readonly IFileManagementClient _fileManagementClient;

    public CommitActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient)
        : base(invocationContext)
    {
        _fileManagementClient = fileManagementClient;
    }

    [Action("Search commits", Description = "Search repository commits")]
    public async Task<ListRepositoryCommitsResponse> ListRepositoryCommits(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] SearchCommitsRequest searchRequest)
    {
        var commits = await SearchRepositoryCommits(repositoryRequest, branchRequest, searchRequest);

        return new()
        {
            Count = commits.Count,
            Commits = commits
        };
    }

    [Action("Find commit", Description = "Find first matching repository commit")]
    public async Task<Commit> FindCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] SearchCommitsRequest searchRequest)
        => (await SearchRepositoryCommits(repositoryRequest, branchRequest, searchRequest)).FirstOrDefault()
           ?? throw new PluginApplicationException("No matching commit was found.");

    private async Task<List<Commit>> SearchRepositoryCommits(
        GetRepositoryRequest repositoryRequest,
        GetOptionalBranchRequest branchRequest,
        SearchCommitsRequest searchRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/commits", Method.Get);
        request.AddQueryParameter("per_page", "100");

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref_name", branchRequest.Name);

        if (searchRequest.CommitAfter.HasValue)
            request.AddQueryParameter("since", FormatGitLabDate(searchRequest.CommitAfter.Value));

        if (searchRequest.CommitBefore.HasValue)
            request.AddQueryParameter("until", FormatGitLabDate(searchRequest.CommitBefore.Value));

        if (!string.IsNullOrWhiteSpace(searchRequest.FilePath))
            request.AddQueryParameter("path", searchRequest.FilePath);

        var includedAuthors = NormalizeFilterValues(searchRequest.AuthorsToInclude).ToList();
        if (includedAuthors.Count == 1)
            request.AddQueryParameter("author", includedAuthors[0]);

        var commits = await RestClient.ExecuteWithErrorHandling<List<Commit>>(request);
        return FilterCommits(commits, searchRequest, includedAuthors).ToList();
    }

    [Action("Get commit", Description = "Get commit by id")]
    public async Task<Commit> GetCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetCommitRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest(
            $"/projects/{projectId}/repository/commits/{Uri.EscapeDataString(input.CommitId)}",
            Method.Get);

        return await RestClient.ExecuteWithErrorHandling<Commit>(request);
    }

    [Action("List added or modified files in X hours", Description = "List added or modified files in X hours")]
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

    [Action("Create or update file", Description = "Create or update file")]
    public async Task<CommitDto> PushFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);

        var repContent =
            await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
                repositoryRequest, branchRequest, new FolderContentWithTypeRequest { IncludeSubfolders = true });

        if (repContent.Content.Where(x => x.Type == GitLabItemTypes.Blob).Any(p => p.Path == input.DestinationFilePath))
        {
            return await UpdateFile(
                repositoryRequest,
                branchRequest,
                new PushFileRequest
                {
                    DestinationFilePath = input.DestinationFilePath,
                    File = input.File,
                    CommitMessage = input.CommitMessage
                });
        }

        if (repContent.Content.Where(x => x.Type == GitLabItemTypes.Tree).Select(x => x.Path)
            .Contains(input.DestinationFilePath.Trim('/')))
        {
            throw new PluginMisconfigurationException("Destination file path is invalid!");
        }

        var file = await _fileManagementClient.DownloadAsync(input.File);
        var fileBytes = await file.GetByteData();
        var pushFileResult = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
            input.DestinationFilePath, fileBytes, GitLabCommitActions.Create);

        return new(pushFileResult);
    }

    [Action("Update file", Description = "Update file in repository")]
    public async Task<CommitDto> UpdateFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);

        var repContent =
            await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
                repositoryRequest, branchRequest,
                new FolderContentWithTypeRequest { IncludeSubfolders = true, ContentType = GitLabItemTypes.Blob });

        if (!repContent.Content.Select(x => x.Path).Contains(input.DestinationFilePath.Trim('/')))
            throw new PluginApplicationException("File does not exist by specified file path!");

        var file = await _fileManagementClient.DownloadAsync(input.File);
        var fileBytes = await file.GetByteData();
        var fileUpload = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
            input.DestinationFilePath, fileBytes, GitLabCommitActions.Update);

        return new(fileUpload);
    }

    [Action("Delete file", Description = "Delete file from repository")]
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
            .Where(commit => includedAuthors.Count == 0 || AuthorMatches(commit, includedAuthors))
            .Where(commit => excludedAuthors.Count == 0 || !AuthorMatches(commit, excludedAuthors))
            .Where(commit => string.IsNullOrWhiteSpace(messageFilter) || CommitMessageMatches(commit, messageFilter));
    }

    private static IEnumerable<string> NormalizeFilterValues(IEnumerable<string>? values)
        => values?
               .Where(value => !string.IsNullOrWhiteSpace(value))
               .Select(value => value.Trim())
           ?? Enumerable.Empty<string>();

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
