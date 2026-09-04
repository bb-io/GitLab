using Apps.Gitlab.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class GitLabLookupDataHandlerTests
{
    [TestMethod]
    public async Task RepositoryPicker_ReturnsMoreThanTwentyWithNamespaceLabels()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var response = request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
                ? GitLabTestData.JsonFixture("projects/projects-page-two.json")
                : GitLabTestData.JsonFixture("projects/projects-page-one.json");
            if (!request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal))
                GitLabTestData.WithHeader(response, "Link",
                    "<https://gitlab.test/api/v4/projects?page=2>; rel=\"next\"");
            return Task.FromResult(response);
        });
        var client = GitLabTestData.CreateClient(handler);
        var picker = new RepositoryDataHandler(GitLabTestData.CreateContext(), client);

        var result = await picker.GetDataAsync(
            new DataSourceContext { SearchString = "repo" }, CancellationToken.None);

        Assert.HasCount(22, result);
        Assert.AreEqual("alpha/repo-01", result["1"]);
        Assert.AreEqual("beta/repo-22", result["22"]);
        var firstQuery = handler.RequestUris.First().Query;
        StringAssert.Contains(firstQuery, "membership=true");
        StringAssert.Contains(firstQuery, "simple=true");
        StringAssert.Contains(firstQuery, "search_namespaces=true");
        StringAssert.Contains(firstQuery, "per_page=100");
        StringAssert.Contains(firstQuery, "search=repo");
    }

    [TestMethod]
    public async Task UserPicker_FollowsPaginationAndUsesStableIds()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            var response = request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
                ? GitLabTestData.JsonFixture("users/users-page-two.json")
                : GitLabTestData.JsonFixture("users/users-page-one.json");
            if (!request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal))
                GitLabTestData.WithHeader(response, "Link",
                    "<https://gitlab.test/api/v4/users?page=2>; rel=\"next\"");
            return Task.FromResult(response);
        });
        var client = GitLabTestData.CreateClient(handler);
        var picker = new UsersDataHandler(GitLabTestData.CreateContext(), client);

        var result = await picker.GetDataAsync(
            new DataSourceContext { SearchString = "user" }, CancellationToken.None);

        Assert.HasCount(3, result);
        Assert.AreEqual("translator (Translation User)", result["42"]);
        Assert.AreEqual("reviewer (Review User)", result["126"]);
        StringAssert.Contains(handler.RequestUris.First().Query, "search=user");
        StringAssert.Contains(handler.RequestUris.First().Query, "per_page=100");
    }
}
