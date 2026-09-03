using System.Net;
using Apps.GitLab.Webhooks;
using Apps.GitLab.Webhooks.Payloads;
using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;
using GitLabCommit = Apps.Gitlab.Webhooks.Payloads.Commit;
using GitLabProject = Apps.Gitlab.Webhooks.Payloads.Project;

namespace Tests.GitLab;

[TestClass]
public class GroupPushWebhookTests
{
    [TestMethod]
    public async Task NoOptionalFilters_ReturnsRequiredOutputAndAllReceivedCommits()
    {
        var response = await Invoke(new CrossRepositoryFileModifiedInput(),
            [
                CreateCommit("first", "skip.txt", "translations/en.po", "translations/en.po"),
                CreateCommit("second", "other.txt")
            ],
            reference: "refs/heads/feature/localization",
            totalCommitCount: 2);

        Assert.AreEqual(HttpStatusCode.OK, response.HttpResponseMessage.StatusCode);
        Assert.IsNotNull(response.Result);
        Assert.AreEqual(101, response.Result.RepositoryId);
        Assert.AreEqual("owner/repository", response.Result.RepositoryNameWithNamespace);
        Assert.AreEqual("feature/localization", response.Result.BranchName);
        Assert.AreEqual(42, response.Result.PushUserId);
        CollectionAssert.AreEqual(new[] { "skip.txt", "translations/en.po", "other.txt" },
            response.Result.FilePathsMatched.ToArray());
        Assert.HasCount(2, response.Result.Commits);
        Assert.AreEqual("hash-first", response.Result.Commits.First().Hash);
        Assert.IsFalse(response.Result.CommitsTruncated);
    }

    [TestMethod]
    public async Task RepositoryAndPushUserFilters_ApplyIgnorePrecedenceToWholePush()
    {
        var commit = CreateCommit("translate", "locales/en.po");

        Assert.IsNotNull((await Invoke(new CrossRepositoryFileModifiedInput
        {
            RepositoryIds = ["101"],
            UserIds = ["42"]
        }, [commit])).Result);

        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            RepositoryIds = ["999"]
        }, [commit]));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            RepositoryIds = ["101"],
            IgnoredRepositoryIds = ["101"]
        }, [commit]));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            UserIds = ["999"]
        }, [commit]));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            UserIds = ["42"],
            IgnoredUserIds = ["42"]
        }, [commit]));
    }

    [TestMethod]
    public async Task BranchFilters_AreCaseSensitiveAndIgnoreWins()
    {
        var commit = CreateCommit("translate", "locales/en.po");

        Assert.IsNotNull((await Invoke(new CrossRepositoryFileModifiedInput
        {
            BranchPatterns = ["^release/.+$"]
        }, [commit], "refs/heads/release/v2")).Result);
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            BranchPatterns = ["^Release/.+$"]
        }, [commit], "refs/heads/release/v2"));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            BranchPatterns = ["^release/.+$"],
            IgnoredBranchPatterns = ["v2$"]
        }, [commit], "refs/heads/release/v2"));
    }

    [TestMethod]
    public async Task MessageAndFileMustMatchSameCommit_AndExcludedCommitDoesNotBlockAnother()
    {
        var splitMatch = await Invoke(new CrossRepositoryFileModifiedInput
        {
            IncludedCommitMessagePatterns = ["translate"],
            FilePatterns = ["*.po"]
        },
        [
            CreateCommit("translate strings", "README.md"),
            CreateCommit("update assets", "locales/en.po")
        ]);
        AssertPreflight(splitMatch);

        var exclusion = await Invoke(new CrossRepositoryFileModifiedInput
        {
            ExcludedCommitMessagePatterns = ["skip"],
            FilePatterns = ["*.po"]
        },
        [
            CreateCommit("skip generated", "locales/ignored.po"),
            CreateCommit("translate", "locales/included.po")
        ]);
        Assert.IsNotNull(exclusion.Result);
        CollectionAssert.AreEqual(new[] { "locales/included.po" }, exclusion.Result.FilePathsMatched.ToArray());
        Assert.HasCount(2, exclusion.Result.Commits);
    }

    [TestMethod]
    public async Task BareFilenameMatchesAnyDepth_AndOnlyModifiedPathsQualify()
    {
        var response = await Invoke(new CrossRepositoryFileModifiedInput { FilePatterns = ["en.po"] },
            [CreateCommit("translate", modified: ["nested/deep/en.po"], added: ["en.po"], removed: ["old/en.po"])]);

        Assert.IsNotNull(response.Result);
        CollectionAssert.AreEqual(new[] { "nested/deep/en.po" }, response.Result.FilePathsMatched.ToArray());

        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput { FilePatterns = ["en.po"] },
            [CreateCommit("rename", modified: [], added: ["new/en.po"], removed: ["old/en.po"])]));
    }

    [TestMethod]
    public async Task PayloadIsCappedAtTwentyWithoutEnrichment()
    {
        var commits = Enumerable.Range(1, 21)
            .Select(x => CreateCommit($"commit-{x}", x == 21 ? "match.po" : "other.txt"))
            .ToArray();
        var noMatch = await Invoke(new CrossRepositoryFileModifiedInput { FilePatterns = ["match.po"] }, commits,
            totalCommitCount: 25);
        AssertPreflight(noMatch);

        commits[0] = CreateCommit("commit-1", "match.po");
        var response = await Invoke(new CrossRepositoryFileModifiedInput { FilePatterns = ["match.po"] }, commits,
            totalCommitCount: 25);
        Assert.IsNotNull(response.Result);
        Assert.HasCount(20, response.Result.Commits);
        Assert.AreEqual(25, response.Result.TotalCommitsCount);
        Assert.IsTrue(response.Result.CommitsTruncated);
    }

    [TestMethod]
    public async Task NonPushNonBranchAndEmptyCreationPayloads_ReturnPreflightWithHttp200()
    {
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput(),
            [CreateCommit("tag", "en.po")], "refs/tags/v1"));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput(), [], "refs/heads/new-branch"));
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput(),
            [CreateCommit("push", "en.po")], objectKind: "merge_request"));
    }

    [TestMethod]
    public async Task InvalidAndTimingOutRegexes_ReturnPreflightWithHttp200()
    {
        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            BranchPatterns = ["["]
        }, [CreateCommit("translate", "en.po")]));

        AssertPreflight(await Invoke(new CrossRepositoryFileModifiedInput
        {
            IncludedCommitMessagePatterns = ["(a+)+$"]
        }, [CreateCommit(new string('a', 100_000) + "!", "en.po")]));
    }

    private static GitLabCommit CreateCommit(string message, params string[] modified)
    {
        return CreateCommit(message, modified, [], []);
    }

    private static GitLabCommit CreateCommit(string message, string[]? modified = null,
        string[]? added = null, string[]? removed = null)
    {
        return new GitLabCommit
        {
            Id = $"hash-{message}",
            Message = message,
            Author = new Author { Name = "Author", Email = "author@example.com" },
            Modified = modified?.ToList() ?? [],
            Added = added?.ToList() ?? [],
            Removed = removed?.ToList() ?? []
        };
    }

    private static Task<WebhookResponse<CrossRepositoryFileModifiedResponse>> Invoke(
        CrossRepositoryFileModifiedInput input, IEnumerable<GitLabCommit> commits,
        string reference = "refs/heads/main", int? totalCommitCount = null, string objectKind = "push")
    {
        var payload = new PushPayload
        {
            ObjectKind = objectKind,
            EventName = objectKind,
            Ref = reference,
            ProjectId = 101,
            UserId = 42,
            Project = new GitLabProject { PathWithNamespace = "owner/repository" },
            Commits = commits.ToList(),
            TotalCommitsCount = totalCommitCount ?? commits.Count()
        };
        var webhook = new GroupPushWebhooks(new InvocationContext());
        return webhook.FilesModifiedInGroups(new WebhookRequest
        {
            Body = JsonConvert.SerializeObject(payload)
        }, new GroupWebhookInput { GroupIds = ["1"] }, input);
    }

    private static void AssertPreflight(WebhookResponse<CrossRepositoryFileModifiedResponse> response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.HttpResponseMessage.StatusCode);
        Assert.IsNull(response.Result);
        Assert.AreEqual(WebhookRequestType.Preflight, response.ReceivedWebhookRequestType);
    }
}
