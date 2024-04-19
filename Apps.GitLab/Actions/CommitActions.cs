using Apps.Gitlab.Actions.Base;
using Apps.Gitlab.Dtos;
using Apps.Gitlab.Models.Commit.Requests;
using Apps.Gitlab.Models.Commit.Responses;
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
using GitLabApiClient.Models.Uploads.Requests;
using GitLabApiClient.Models.Uploads.Responses;

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
    public async Task<Upload> PushFile(
        [ActionParameter] GetRepositoryRequest repositoryRequest,
        [ActionParameter] GetOptionalBranchRequest branchRequest,
        [ActionParameter] PushFileRequest input)
    {
        var projectId = (ProjectId)int.Parse(repositoryRequest.RepositoryId);
        var repContent = await new RepositoryActions(InvocationContext, _fileManagementClient).ListRepositoryContent(
           repositoryRequest, branchRequest, new FolderContentRequest() { IncludeSubfolders = true });
        //if (repContent.Content.Any(p => p.Path == input.DestinationFilePath)) // update in case of existing file
        //{
        //    return UpdateFile(
        //        repositoryRequest,
        //        branchRequest,
        //        new()
        //        {
        //            FileId = repContent.Items.First(p => p.Path == input.DestinationFilePath).Sha,
        //            DestinationFilePath = input.DestinationFilePath,
        //            File = input.File,
        //            CommitMessage = input.CommitMessage
        //        });
        //}

        var file = _fileManagementClient.DownloadAsync(input.File).Result;
        var fileBytes = file.GetByteData().Result;
        var pushFileResult = await Client.Uploads.UploadFile(projectId, new CreateUploadRequest(file, input.DestinationFilePath));
        return pushFileResult;
    }

    //[Action("Update file", Description = "Update file in repository")]
    //public SmallCommitDto UpdateFile(
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] GetOptionalBranchRequest branchRequest,
    //    [ActionParameter] Models.Commit.Requests.UpdateFileRequest input)
    //{
    //    var fileId = input.FileId ?? GetFileId(repositoryRequest.RepositoryId, input.DestinationFilePath, branchRequest);
    //    var file = _fileManagementClient.DownloadAsync(input.File).Result;
    //    var fileBytes = file.GetByteData().Result;
    //    var fileUpload = new Octokit.UpdateFileRequest(input.CommitMessage, Convert.ToBase64String(fileBytes), fileId, branchRequest.Name,
    //        false);
    //    var pushFileResult = Client.Repository.Content
    //        .UpdateFile(long.Parse(repositoryRequest.RepositoryId), input.DestinationFilePath, fileUpload).Result;

    //    return new(pushFileResult.Commit);
    //}

    //[Action("Delete file", Description = "Delete file from repository")]
    //public Task DeleteFile(
    //    [ActionParameter] GetRepositoryRequest repositoryRequest,
    //    [ActionParameter] GetOptionalBranchRequest branchRequest,
    //    [ActionParameter] Models.Commit.Requests.DeleteFileRequest input)
    //{
    //    var fileId = GetFileId(repositoryRequest.RepositoryId, input.FilePath, branchRequest);

    //    var fileDelete = new Octokit.DeleteFileRequest(input.CommitMessage, fileId, branchRequest.Name);
    //    return Client.Repository.Content.DeleteFile(long.Parse(repositoryRequest.RepositoryId), input.FilePath, fileDelete);
    //}

    //private string GetFileId(string repoId, string path, GetOptionalBranchRequest branchRequest)
    //{
    //    var repoContent = new RepositoryActions(InvocationContext, _fileManagementClient).ListAllRepositoryContent(new()
    //    {
    //        RepositoryId = repoId
    //    }, branchRequest);

    //    return repoContent.Items.First(x => x.Path == path).Sha;
    //}
}