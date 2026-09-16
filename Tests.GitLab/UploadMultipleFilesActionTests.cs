using Apps.Gitlab.Actions;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Models.Commit.Requests;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class UploadMultipleFilesActionTests
{
    [TestMethod]
    public async Task PushFiles_WithNoFiles_ThrowsBeforeCallingGitLab()
    {
        var action = CreateAction();

        var exception = await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() => action.PushFiles(
            RepositoryRequest(),
            new GetOptionalBranchRequest(),
            new PushFilesRequest()));

        StringAssert.Contains(exception.Message, "At least one file");
    }

    [TestMethod]
    public async Task PushFiles_WithDifferentFileAndPathCounts_ThrowsBeforeCallingGitLab()
    {
        var action = CreateAction();
        var input = new PushFilesRequest
        {
            Files = [new FileReference { Name = "one.txt", ContentType = "text/plain" }],
            DestinationFilePaths = []
        };

        var exception = await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() => action.PushFiles(
            RepositoryRequest(),
            new GetOptionalBranchRequest(),
            input));

        StringAssert.Contains(exception.Message, "number of files");
    }

    [TestMethod]
    public async Task PushFiles_WithDuplicateNormalizedPaths_ThrowsBeforeCallingGitLab()
    {
        var action = CreateAction();
        var input = new PushFilesRequest
        {
            Files =
            [
                new FileReference { Name = "one.txt", ContentType = "text/plain" },
                new FileReference { Name = "two.txt", ContentType = "text/plain" }
            ],
            DestinationFilePaths = ["locales/file.txt", " /locales/file.txt/ "]
        };

        var exception = await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() => action.PushFiles(
            RepositoryRequest(),
            new GetOptionalBranchRequest(),
            input));

        StringAssert.Contains(exception.Message, "must be unique");
    }

    private static CommitActions CreateAction() =>
        new(GitLabTestData.CreateContext(), new FileManagementClient("."));

    private static GetRepositoryRequest RepositoryRequest() => new() { RepositoryId = "101" };
}
