using Apps.Gitlab.Actions;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Constants;
using Apps.GitLab.Models.Commit.Requests;
using Blackbird.Applications.Sdk.Common.Invocation;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class CommitActionTests : TestBaseWithContext
{
    private const string CollectingReferencesDemoRepositoryId = "83936767";

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithRepository_ReturnsCommits(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest());

        Assert.IsTrue(result.Commits.Any());
        Assert.AreEqual(result.Commits.Count(), result.Count);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithAuthorInclude_ReturnsMatchingAuthor(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                AuthorsToInclude = ["Localization Blackbird"]
            });

        Assert.IsTrue(result.Commits.Any());
        Assert.IsTrue(result.Commits.All(commit =>
            (!string.IsNullOrWhiteSpace(commit.AuthorName) &&
             commit.AuthorName.Contains("Localization Blackbird", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(commit.AuthorEmail) &&
             commit.AuthorEmail.Contains("Localization Blackbird", StringComparison.OrdinalIgnoreCase))));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithDateRange_ReturnsCommitsInRange(InvocationContext context)
    {
        var start = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                CommitAfter = start,
                CommitBefore = end
            });

        Assert.IsTrue(result.Commits.Any());
        Assert.IsTrue(result.Commits.All(commit => commit.CreatedAt >= start && commit.CreatedAt <= end));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithMessage_ReturnsMatchingMessage(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                CommitMessageContains = "Chore:"
            });

        Assert.IsTrue(result.Commits.Any());
        Assert.IsTrue(result.Commits.All(commit =>
            (!string.IsNullOrWhiteSpace(commit.Message) &&
             commit.Message.Contains("Chore:", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(commit.Title) &&
             commit.Title.Contains("Chore:", StringComparison.OrdinalIgnoreCase))));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithAuthorExclude_RemovesMatchingAuthor(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                AuthorsToExclude = ["Localization Blackbird"]
            });

        Assert.IsTrue(result.Commits.Any());
        Assert.IsTrue(result.Commits.All(commit =>
            (string.IsNullOrWhiteSpace(commit.AuthorName) ||
             !commit.AuthorName.Contains("Localization Blackbird", StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(commit.AuthorEmail) ||
             !commit.AuthorEmail.Contains("Localization Blackbird", StringComparison.OrdinalIgnoreCase))));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task SearchCommits_WithFilePath_ReturnsCommits(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                FilePath = "changelog.md"
            });

        Assert.IsTrue(result.Commits.Any());
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task FindCommit_WithMessage_ReturnsFirstMatchingCommit(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var result = await action.FindCommit(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            new SearchCommitsRequest
            {
                CommitMessageContains = "Chore:"
            });

        Assert.IsNotNull(result);
        Assert.IsTrue(
            (!string.IsNullOrWhiteSpace(result.Message) &&
             result.Message.Contains("Chore:", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(result.Title) &&
             result.Title.Contains("Chore:", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken, ConnectionTypes.OAuth)]
    public async Task FindCommit_WithSameFilters_ReturnsFirstSearchResult(InvocationContext context)
    {
        var action = new CommitActions(context, FileManagementClient);
        var searchRequest = new SearchCommitsRequest
        {
            AuthorsToInclude = ["Localization Blackbird"],
            CommitMessageContains = "Chore:"
        };

        var searchResult = await action.ListRepositoryCommits(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            searchRequest);
        var findResult = await action.FindCommit(
            new GetRepositoryRequest { RepositoryId = CollectingReferencesDemoRepositoryId },
            new GetOptionalBranchRequest { Name = "main" },
            searchRequest);

        Assert.IsTrue(searchResult.Commits.Any());
        Assert.AreEqual(searchResult.Commits.First().Id, findResult.Id);
    }
}
