using Apps.Gitlab.Actions;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Constants;
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
}

