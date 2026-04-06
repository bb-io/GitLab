using Apps.Gitlab.DataSourceHandlers;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class PickerDataHandlerTests : TestBaseWithContext
{
    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken)]
    public async Task FilePicker_GetFolderContentAsync_ReturnsMappedRootItems(InvocationContext context)
    {
        var handler = new FilePickerDataHandler(context, new Apps.Gitlab.Models.Respository.Requests.GetRepositoryRequest { RepositoryId= "71835863" },
            new Apps.Gitlab.Models.Branch.Requests.GetOptionalBranchRequest { });

        var result = await handler.GetFolderContentAsync(new FolderContentDataSourceContext
        {
           
        }, CancellationToken.None);

        foreach (var item in result)
        {
            Console.WriteLine($"Name: {item.DisplayName}, Id: {item.Id}");
        }

        Assert.IsNotNull(result);
    }
}
