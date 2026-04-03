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

    [Action("List commits", Description = "List respository commits")]
    public async Task<ListRepositoryCommitsResponse> ListRepositoryCommits(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest)
    {
        var projectId = ParseProjectId(repositoryRequest.RepositoryId);
        var request = RestClient.CreateRequest($"/projects/{projectId}/repository/commits", Method.Get);

        if (!string.IsNullOrWhiteSpace(branchRequest.Name))
            request.AddQueryParameter("ref_name", branchRequest.Name);

        var commits = await RestClient.ExecuteWithErrorHandling<List<Commit>>(request);
        return new()
        {
            Commits = commits
        };
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
}
