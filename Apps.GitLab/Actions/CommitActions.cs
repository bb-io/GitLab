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
        var commits = await Client.Commits.GetAsync(projectId,
             (CommitQueryOptions options) =>
             {
                 options.All = true;
                 if(!string.IsNullOrWhiteSpace(branchRequest.Name))
                    options.RefName = branchRequest.Name;
             });
        return new()
        {
            Commits = commits
        };
    }

    [Action("Get commit", Description = "Get commit by id")]
    public Commit GetCommit(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetCommitRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var commit = Client.Commits.GetAsync(projectId, input.CommitId).Result;
        return commit;
    }

    [Action("Create or update file", Description = "Create or update file")]
    public async Task<Commit> PushFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var repContent = await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
           repositoryRequest, branchRequest, new FolderContentRequest() { IncludeSubfolders = true });
        if (repContent.Content.Any(p => p.Path == input.DestinationFilePath)) // update in case of existing file
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

        var file = _fileManagementClient.DownloadAsync(input.File).Result;
        var fileBytes = file.GetByteData().Result;
        var pushFileResult = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage, input.DestinationFilePath, fileBytes, "create");
        return pushFileResult;
    }

    [Action("Update file", Description = "Update file in repository")]
    public async Task<Commit> UpdateFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] Models.Commit.Requests.UpdateFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var file = _fileManagementClient.DownloadAsync(input.File).Result;
        var fileBytes = file.GetByteData().Result;
        var fileUpload = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage, input.DestinationFilePath, fileBytes, "update");
        return fileUpload;
    }

    [Action("Delete file", Description = "Delete file from repository")]
    public async Task<Commit> DeleteFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] Models.Commit.Requests.DeleteFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var fileDelete = await RestClient.PushChanges(projectId, branchRequest.Name, input.CommitMessage, input.FilePath, null, "delete");
        return fileDelete;
    }
}