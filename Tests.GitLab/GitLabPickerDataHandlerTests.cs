using Apps.Gitlab.DataSourceHandlers;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class PickerDataHandlerTests : TestBaseWithContext
{
    private const string RepositoryId = "83929674";
    private const string BranchName = "codex/picker-pagination-repro-20260811";

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth, ConnectionTypes.PersonalAccessToken)]
    public async Task FilePicker_GetFolderContentAsync_ReturnsItemsFromAllPages(InvocationContext context)
    {
        var handler = new FilePickerDataHandler(
            context,
            new() { RepositoryId = RepositoryId },
            new() { Name = BranchName });

        var result = await handler.GetFolderContentAsync(new FolderContentDataSourceContext
        {
            FolderId = "pagination-repro-files"
        }, CancellationToken.None);

        var items = result.ToList();

        Assert.HasCount(150, items);
        Assert.IsTrue(items.Any(x => x.DisplayName == "file-150.txt"));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.OAuth, ConnectionTypes.PersonalAccessToken)]
    public async Task FolderPicker_GetFolderContentAsync_ReturnsItemsFromAllPages(InvocationContext context)
    {
        var handler = new FolderPickerDataHandler(
            context,
            new() { RepositoryId = RepositoryId },
            new() { Name = BranchName });

        var result = await handler.GetFolderContentAsync(new FolderContentDataSourceContext
        {
            FolderId = "pagination-repro-folders"
        }, CancellationToken.None);

        var items = result.ToList();

        Assert.HasCount(150, items);
        Assert.IsTrue(items.Any(x => x.DisplayName == "folder-150"));
    }
}
