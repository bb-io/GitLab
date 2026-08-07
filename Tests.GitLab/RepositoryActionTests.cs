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
    }
}

