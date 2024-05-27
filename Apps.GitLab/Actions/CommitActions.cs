using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Models.Commit.Requests;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.Gitlab.Models.Branch.Requests;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.Commits.Requests;
using GitLabApiClient.Models.Commits.Responses;
using Apps.GitLab.Models.Commit.Requests;
using Apps.Gitlab.Webhooks;
using Apps.GitLab.Models.Commit.Responses;
using Apps.GitLab.Dtos;
using GitLabApiClient;
using Apps.GitLab.Models.Respository.Requests;

namespace Apps.Gitlab.Actions;

[ActionList]
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var commits = await Client.Commits.GetAsync(projectId,
                options =>
                {
                    if (!string.IsNullOrWhiteSpace(branchRequest.Name))
                        options.RefName = branchRequest.Name;
                });
            return new()
            {
                Commits = commits
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Get commit", Description = "Get commit by id")]
    public Commit GetCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetCommitRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var commit = Client.Commits.GetAsync(projectId, input.CommitId).Result;
            return commit;
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
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
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var commits = await Client.Commits.GetAsync(projectId,
                options =>
                {
                    options.Since = DateTime.Now.AddHours(-hoursRequest.Hours);
                    if (!string.IsNullOrWhiteSpace(branchRequest.Name))
                        options.RefName = branchRequest.Name;
                });
            var files = new List<AddedOrModifiedFile>();
            commits.ToList().ForEach(c =>
            {
                var commit = Client.Commits.GetDiffsAsync(projectId, c.Id).Result;
                files.AddRange(commit.Where(x => !x.IsDeletedFile).Where(f =>
                        string.IsNullOrEmpty(folderInput.FolderPath) ||
                        PushWebhooks.IsFilePathMatchingPattern(folderInput.FolderPath, f.NewPath))
                    .Select(x => new AddedOrModifiedFile(x)));
            });
            return new ListAddedOrModifiedInHoursResponse() { Files = files.DistinctBy(x => x.Filename).ToList() };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Create or update file", Description = "Create or update file")]
    public async Task<CommitDto> PushFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var repContent =
                await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
                    repositoryRequest, branchRequest, new FolderContentWithTypeRequest() { IncludeSubfolders = true });
            if (repContent.Content.Where(x => x.Type == "blob")
                .Any(p => p.Path == input.DestinationFilePath)) // update in case of existing file
            {
                return await UpdateFile(
                    repositoryRequest,
                    branchRequest,
                    new()
                    {
                        DestinationFilePath = input.DestinationFilePath,
                        File = input.File,
                        CommitMessage = input.CommitMessage
                    });
            }

            if (repContent.Content.Where(x => x.Type == "tree").Select(x => x.Path)
                .Contains(input.DestinationFilePath.Trim('/')))
                throw new GitLabFriendlyException("Destination file path is invalid!");

            var file = await _fileManagementClient.DownloadAsync(input.File);
            var fileBytes = await file.GetByteData();
            var pushFileResult = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
                input.DestinationFilePath, fileBytes, "create");
            
            return new(pushFileResult);
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Update file", Description = "Update file in repository")]
    public async Task<CommitDto> UpdateFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var repContent =
                await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
                    repositoryRequest, branchRequest,
                    new FolderContentWithTypeRequest() { IncludeSubfolders = true, ContentType = "blob" });
            if (!repContent.Content.Select(x => x.Path).Contains(input.DestinationFilePath.Trim('/')))
                throw new GitLabFriendlyException("File does not exist by specified file path!");

            var file = await _fileManagementClient.DownloadAsync(input.File);
            var fileBytes = await file.GetByteData();
            var fileUpload = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
                input.DestinationFilePath, fileBytes, "update");
            return new(fileUpload);
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }

    [Action("Delete file", Description = "Delete file from repository")]
    public async Task<DeleteFileResponse> DeleteFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] DeleteFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        try
        {
            var fileDelete = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage,
                input.FilePath, null, "delete");
            return new()
            {
                CommitId = fileDelete.Id,
                Title = fileDelete.Title,
                Message = fileDelete.Message
            };
        }
        catch (GitLabException ex)
        {
            throw new GitLabFriendlyException(ex.Message);
        }
    }
}