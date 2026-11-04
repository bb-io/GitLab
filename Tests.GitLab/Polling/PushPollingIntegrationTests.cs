using Apps.Gitlab;
using System.Globalization;
using Apps.GitLab.Constants;
using Apps.GitLab.Polling;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;

namespace Tests.GitLab.Polling;

[TestClass]
public class PushPollingIntegrationTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReporterConnection_CompletesReadOnlyHappyPath()
    {
        var token = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_TOKEN");
        var repositoryId = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_REPOSITORY_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(repositoryId))
            Assert.Inconclusive(
                "Set GITLAB_REPORTER_TEST_TOKEN and GITLAB_REPORTER_TEST_REPOSITORY_ID to run Reporter-role test.");

        var credentials = new AuthenticationCredentialsProvider[]
        {
            new(CredNames.ConnectionType, ConnectionTypes.PersonalAccessToken),
            new(CredNames.Authorization, token!)
        };
        var context = new InvocationContext { AuthenticationCredentialsProviders = credentials };
        var pollTime = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_POLL_TIME_UTC");
        var now = string.IsNullOrWhiteSpace(pollTime)
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.Parse(pollTime, CultureInfo.InvariantCulture).ToUniversalTime();
        var polling = new PushPollingList(context, new BlackbirdGitlabClient(credentials), () => now);
        var input = new FilesModifiedPollingInput { RepositoryIds = [repositoryId!] };
        var baseline = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory> { PollingTime = now.UtcDateTime }, input);
        baseline.Memory!.RecentProcessedKeys = [];

        var response = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = baseline.Memory,
                PollingTime = now.UtcDateTime
            }, input);

        Assert.IsTrue(response.FlyBird,
            "Reporter fixture repository needs a qualifying branch push within 24 hours before the test poll time.");
        Assert.IsNotNull(response.Result);
        Assert.IsTrue(response.Result.Pushes.Any(push => push.RepositoryId.ToString() == repositoryId));

        var expectedCommit = Environment.GetEnvironmentVariable("GITLAB_REPORTER_TEST_EXPECTED_COMMIT");
        if (!string.IsNullOrWhiteSpace(expectedCommit))
            Assert.IsTrue(response.Result.Pushes.Any(push => push.Commits.Any(commit => commit.Hash == expectedCommit)));
        foreach (var push in response.Result.Pushes)
            TestContext?.WriteLine($"Repository {push.RepositoryId}, branch {push.BranchName}, " +
                                   $"matched files: {string.Join(", ", push.FilePathsMatched)}; " +
                                   $"commits: {string.Join(", ", push.Commits.Select(commit => commit.Hash))}");

        var repeated = await polling.OnFilesModifiedAcrossRepositories(
            new PollingEventRequest<PushPollingMemory>
            {
                Memory = response.Memory,
                PollingTime = now.UtcDateTime
            }, input);
        Assert.IsFalse(repeated.FlyBird, "Repeating the scan must not emit processed pushes again.");
        Assert.IsNull(repeated.Result);
    }
}
