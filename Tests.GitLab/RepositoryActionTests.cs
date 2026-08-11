using Apps.Gitlab.Actions;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Constants;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.GitLab.Models.Respository.Requests;
using Blackbird.Applications.Sdk.Common.Invocation;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class RepositoryActionTests : TestBaseWithContext
{
    private const string LiveTestRepositoryId = "83929674";
    private const string LiveTestBranch = "codex/content-id-metadata-live-tests-20260811";

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken)]
    public async Task GetRepository_WithExistingRepository_ReturnsRepository(InvocationContext context)
    {
      var action = new RepositoryActions(context, FileManagementClient);

        var result = await action.GetRepositoryById(new GetRepositoryRequest
        {
            RepositoryId = "71835863"
        });

        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        Assert.IsNotNull(result);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken)]
    public async Task CreateRepository_WithExistingRepository_ReturnsRepository(InvocationContext context)
    {
        var action = new RepositoryActions(context, FileManagementClient);

        var result = await action.CreateRepository(new CreateRepositoryInput
        {
            Name = "Test Repository",
        });

        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        Assert.IsNotNull(result);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task GetFile_WithExistingFile_ReturnsFile(InvocationContext context)
    {
        // Arrange
        var action = new RepositoryActions(context, FileManagementClient);
        var repoRequest = new GetRepositoryRequest { RepositoryId = "83929674" };
        var branchRequest = new GetOptionalBranchRequest { };
        var fileRequest = new GetFileRequest { FilePath = "locales/en-US/messages.po" };

        // Act
        var result = await action.GetFile(repoRequest, branchRequest, fileRequest);

        // Assert
        PrintResult(result);
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Metadata);
        Assert.AreEqual("en-US", result.Metadata.SourceLanguage);
        Assert.IsTrue(result.Metadata.DateChanged > DateTimeOffset.MinValue);
        Assert.AreEqual("Gitlab", result.Metadata.SystemReference.SystemName);
        Assert.AreEqual(
            "localizationblackbird/collecting-references-demo:locales/en-US/messages.po",
            result.Metadata.SystemReference.ContentId);
        Assert.AreEqual("Gitlab", result.Metadata.SourceSystemReference.SystemName);
        Assert.IsNotNull(result.Metadata.Provenance.Translation);
        Assert.IsNotNull(result.Metadata.Provenance.Review);

        var commitActions = new CommitActions(context, FileManagementClient);
        var commits = await commitActions.ListRepositoryCommits(
            repoRequest,
            branchRequest,
            new Apps.GitLab.Models.Commit.Requests.ListCommitsRequest
            {
                FilePath = fileRequest.FilePath,
                MaximumResults = 1
            });
        var latestCommit = commits.Commits.Single();

        Assert.AreEqual(latestCommit.AuthorName, result.Metadata.Provenance.Review.Person);
        Assert.AreEqual(latestCommit.AuthorEmail, result.Metadata.Provenance.Review.PersonReference);
        Assert.AreEqual("GitLab", result.Metadata.Provenance.Review.Tool);
        Assert.AreEqual(latestCommit.WebUrl, result.Metadata.Provenance.Review.ToolReference);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task GetFile_WithContentIdOverride_UsesProvidedContentId(InvocationContext context)
    {
        await EnsureLiveTestBranchExists(context);
        var action = new RepositoryActions(context, FileManagementClient);
        const string contentId = "gitlab-live-test-download-file";

        var result = await action.GetFile(
            new GetRepositoryRequest { RepositoryId = LiveTestRepositoryId },
            new GetOptionalBranchRequest { Name = LiveTestBranch },
            new GetFileRequest
            {
                FilePath = "locales/en-US/messages.po",
                ContentId = contentId
            });

        Assert.IsNotNull(result.Metadata);
        Assert.AreEqual(contentId, result.Metadata.SystemReference.ContentId);
        Assert.AreEqual(contentId, result.Metadata.SourceSystemReference.ContentId);
        Assert.AreEqual(
            "localizationblackbird/collecting-references-demo:locales/en-US/messages.po",
            result.Metadata.SystemReference.ContentName);
        Assert.AreEqual("Gitlab", result.Metadata.SystemReference.SystemName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Metadata.SystemReference.AdminUrl));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Metadata.SystemReference.SystemRef));
        Assert.IsTrue(result.Metadata.DateChanged > DateTimeOffset.MinValue);
        Assert.AreEqual("GitLab", result.Metadata.Provenance.Review.Tool);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task GetAllFilesInFolder_WithInteroperableFiles_AddsReviewProvenance(InvocationContext context)
    {
        var action = new RepositoryActions(context, FileManagementClient);

        var result = await action.GetAllFilesInFolder(
            new GetRepositoryRequest { RepositoryId = "83929674" },
            new GetOptionalBranchRequest(),
            new FolderContentRequest
            {
                Path = "locales/en-US",
                IncludeSubfolders = true
            });

        Assert.IsTrue(result.Metadata.Any());
        Assert.IsTrue(result.Metadata.All(metadata => metadata.Provenance.Review.Tool == "GitLab"));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.Person)));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.PersonReference)));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.ToolReference)));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task GetAllFilesInFolder_WithContentIdOverride_AppliesDownloadFileMetadata(InvocationContext context)
    {
        await EnsureLiveTestBranchExists(context);
        var action = new RepositoryActions(context, FileManagementClient);
        const string contentId = "gitlab-live-test-folder-files";

        var result = await action.GetAllFilesInFolder(
            new GetRepositoryRequest { RepositoryId = LiveTestRepositoryId },
            new GetOptionalBranchRequest { Name = LiveTestBranch },
            new FolderContentRequest
            {
                Path = "locales/en-US",
                IncludeSubfolders = true,
                ContentId = contentId
            });

        Assert.IsTrue(result.Files.Any());
        Assert.IsTrue(result.Files.All(file => file.FilePath.StartsWith("locales/en-US/")));
        Assert.IsTrue(result.Metadata.Any());
        Assert.IsTrue(result.Metadata.All(metadata => metadata.SystemReference.ContentId == contentId));
        Assert.IsTrue(result.Metadata.All(metadata => metadata.SourceSystemReference.ContentId == contentId));
        Assert.IsTrue(result.Metadata.All(metadata =>
            metadata.SystemReference.ContentName?.StartsWith(
                "localizationblackbird/collecting-references-demo:locales/en-US/") == true));
        Assert.IsTrue(result.Metadata.All(metadata => metadata.SystemReference.SystemName == "Gitlab"));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.SystemReference.AdminUrl)));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.SystemReference.SystemRef)));
        Assert.IsTrue(result.Metadata.All(metadata => metadata.DateChanged > DateTimeOffset.MinValue));
        Assert.IsTrue(result.Metadata.All(metadata => metadata.Provenance.Review.Tool == "GitLab"));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.Person)));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.PersonReference)));
        Assert.IsTrue(result.Metadata.All(metadata => !string.IsNullOrWhiteSpace(metadata.Provenance.Review.ToolReference)));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task GetAllFilesInFolder_WithoutSubfolders_RetainsFilesInRequestedFolder(InvocationContext context)
    {
        var action = new RepositoryActions(context, FileManagementClient);

        var result = await action.GetAllFilesInFolder(
            new GetRepositoryRequest { RepositoryId = "83929674" },
            new GetOptionalBranchRequest(),
            new FolderContentRequest
            {
                Path = "locales/en-US",
                IncludeSubfolders = false
            });

        Assert.IsTrue(result.Files.Any());
        Assert.IsTrue(result.Files.All(file =>
            Path.GetDirectoryName(file.FilePath)?.Replace('\\', '/') == "locales/en-US"));
        Assert.IsTrue(result.Metadata.Any());
        Assert.IsTrue(result.Metadata.All(metadata => metadata.Provenance.Review.Tool == "GitLab"));
    }

    private async Task EnsureLiveTestBranchExists(InvocationContext context)
    {
        var repositoryRequest = new GetRepositoryRequest { RepositoryId = LiveTestRepositoryId };
        var repositoryActions = new RepositoryActions(context, FileManagementClient);
        if (await repositoryActions.BranchExists(repositoryRequest, LiveTestBranch))
            return;

        var repository = await repositoryActions.GetRepositoryById(repositoryRequest);
        var branchActions = new BranchActions(context);
        await branchActions.CreateBranch(
            repositoryRequest,
            new CreateBranchRequest
            {
                BaseBranchName = repository.DefaultBranch!,
                NewBranchName = LiveTestBranch
            });
    }
}

