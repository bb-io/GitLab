using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Apps.Gitlab;
using Apps.Gitlab.Models.Branch.Requests;
using Apps.Gitlab.Models.Respository.Requests;
using Apps.GitLab.Actions;
using Apps.GitLab.Configuration;
using Apps.GitLab.Constants;
using Apps.GitLab.Polling;
using Apps.GitLab.Polling.Models;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Polling;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Tests.GitLab.Base;

namespace Tests.GitLab.Polling;

[TestClass]
public class RepositoryConfigurationTests
{
    private const string Mask = "locales/[LOCALE].po";
    private static readonly GetRepositoryRequest ConfigRepository = new() { RepositoryId = "900" };
    private static readonly GetBranchRequest ConfigBranch = new() { Name = "main" };
    private static readonly ConfigurationFileInput ConfigFile = new() { FilePath = "settings/blackbird.json" };
    private static readonly DateTimeOffset Baseline = GitLabTestData.ScanTime;
    private static readonly DateTimeOffset PushTime = Baseline.AddSeconds(30);

    [TestMethod]
    public async Task GetAndSearchUseRepositoryBranchAndMaskAndReturnNestedMetadata()
    {
        var fixture = new Fixture(
            Entry(101),
            Entry(101, mask: "locales/[LOCALE]/messages.po"),
            Entry(101, branch: "release", enabled: false),
            Entry(202));
        var actions = fixture.Actions;
        var results = await actions.Search(ConfigRepository, ConfigBranch, ConfigFile, new());

        Assert.AreEqual(4, results.Configurations.Count);
        Assert.AreEqual("locales/en/messages.po", results.Configurations[1].SourceFilePath);
        Assert.AreEqual("release", results.Configurations[2].Branch);
        Assert.IsFalse(results.Configurations[2].Enabled);
        Assert.AreEqual(202, results.Configurations[3].RepositoryId);
        Assert.AreEqual(4, fixture.Files.Uploads.Count);

        var get = await actions.Get(ConfigRepository, ConfigBranch, ConfigFile,
            new RepositoryConfigurationInput { RepositoryId = "101", Branch = "release", FileMask = Mask });
        Assert.IsFalse(get.Enabled);
        CollectionAssert.AreEqual(new[] { "fr", "pt_BR" }, get.TargetLocales);
        Assert.AreEqual("application/json", fixture.Files.Uploads[get.Metadata.Name].ContentType);
        using var metadata = JsonDocument.Parse(fixture.Files.Uploads[get.Metadata.Name].Text);
        Assert.AreEqual("Localization Team", metadata.RootElement.GetProperty("workflow")[0]
            .GetProperty("department").GetString());

        var filters = new RepositoryConfigurationFilters
        {
            RepositoryId = "101", Branch = "release", FileMask = Mask, SourceLocale = "en",
            TargetLocales = ["fr", "pt_BR"], ContainsMetadata = "PARTmen"
        };
        var found = await actions.Search(ConfigRepository, ConfigBranch, ConfigFile, filters);
        Assert.AreEqual("release", found.Configurations.Single().Branch);
        filters.ContainsMetadata = "tion team";
        Assert.AreEqual(1, (await actions.Search(ConfigRepository, ConfigBranch, ConfigFile, filters)).Configurations.Count);
        filters.TargetLocales = ["fr", "de"];
        Assert.AreEqual(0, (await actions.Search(ConfigRepository, ConfigBranch, ConfigFile, filters)).Configurations.Count);

        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() =>
            actions.Get(ConfigRepository, ConfigBranch, ConfigFile,
                new RepositoryConfigurationInput { RepositoryId = "202", Branch = "release", FileMask = Mask }));
    }

    [TestMethod]
    public async Task InvalidEntriesWarnAndEveryOccurrenceOfADuplicateIsSkipped()
    {
        var first = Entry(101);
        var second = Entry(101);
        second["repositoryId"] = "101";
        second.Remove("enabled");
        var invalidBranch = Entry(201, branch: " \t");
        var invalidSource = Entry(202);
        invalidSource["sourceLocale"] = null;
        var invalidTargets = new[] { "[]", "[\"\"]", "[\"fr\",\"  \"]", "[null]", "[123]", "\"fr\"" }
            .Select((value, index) =>
            {
                var entry = Entry(300 + index);
                entry["targetLocales"] = JsonNode.Parse(value);
                return entry;
            }).ToArray();
        var invalidMetadata = Entry(401);
        invalidMetadata["metadata"] = new JsonArray();
        var invalidMask = Entry(402, mask: "locales/[locale].po");
        var valid = Entry(403);
        valid["sourceLocale"] = "English US";
        var fixture = new Fixture([first, second, invalidBranch, invalidSource, ..invalidTargets,
            invalidMetadata, invalidMask, valid]);

        var found = await fixture.Actions.Search(ConfigRepository, ConfigBranch, ConfigFile, new());

        Assert.AreEqual(403, found.Configurations.Single().RepositoryId);
        Assert.AreEqual("locales/English US.po", found.Configurations.Single().SourceFilePath);
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("duplicate configuration entries 1, 2")));
        foreach (var field in new[] { "enabled", "branch", "sourceLocale", "metadata", "fileMask" })
            Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains($"'{field}'")), field);
        Assert.AreEqual(invalidTargets.Length, fixture.Warnings.Count(warning => warning.Contains("'targetLocales'")));
    }

    [TestMethod]
    public async Task SearchAcceptsDeepNestedMetadataAndFindsItsScalarValue()
    {
        var entry = Entry(101).ToJsonString();
        var metadata = string.Concat(Enumerable.Repeat("{\"nested\":", 80)) +
            "\"three language groups\"" + new string('}', 80);
        var entryWithNestedMetadata = entry[..entry.IndexOf("\"metadata\":", StringComparison.Ordinal)] +
            "\"metadata\":" + metadata + "}";
        var fixture = new Fixture
        {
            ConfigurationOverride = GitLabTestData.Json("[" + entryWithNestedMetadata + "]")
        };

        var result = await fixture.Actions.Search(ConfigRepository, ConfigBranch, ConfigFile,
            new() { ContainsMetadata = "LANGUAGE GROUP" });

        Assert.AreEqual(metadata, fixture.Files.Uploads[result.Configurations.Single().Metadata.Name].Text);
    }

    [TestMethod]
    [DataRow("{}", HttpStatusCode.OK)]
    [DataRow("[", HttpStatusCode.OK)]
    [DataRow("", HttpStatusCode.NotFound)]
    [DataRow("", HttpStatusCode.Forbidden)]
    public async Task UnreadableOrMalformedConfigurationFailsTheRead(string content, HttpStatusCode status)
    {
        var fixture = new Fixture { ConfigurationOverride = GitLabTestData.Json(content, status) };

        await Assert.ThrowsExactlyAsync<PluginMisconfigurationException>(() =>
            fixture.Actions.Search(ConfigRepository, ConfigBranch, ConfigFile, new()));
        Assert.AreEqual(0, fixture.Files.Uploads.Count);
    }

    [TestMethod]
    public async Task PollWarnsAboutUnavailableResourcesAndContinuesWithEnabledRepositories()
    {
        var fixture = new Fixture(
            Entry(101), Entry(102), Entry(103, branch: "missing"), Entry(104, mask: "missing/[LOCALE].po"),
            Entry(105, enabled: false), Entry(106), Entry(107));
        fixture.Route = (method, path) => (method.Method, path) switch
        {
            ("GET", "/projects/101") => GitLabTestData.Json("{}", HttpStatusCode.NotFound),
            ("GET", "/projects/102") => GitLabTestData.Json("{}", HttpStatusCode.Forbidden),
            ("GET", "/projects/103/repository/branches/missing") => GitLabTestData.Json("{}", HttpStatusCode.NotFound),
            ("HEAD", "/projects/104/repository/files/missing/en.po") => GitLabTestData.Json("{}", HttpStatusCode.NotFound),
            ("GET", "/projects/106/events") => GitLabTestData.Json("{}", HttpStatusCode.Forbidden),
            _ => null
        };
        var baseline = await fixture.Poll();
        fixture.Now = Baseline.AddMinutes(1);

        var next = await fixture.Poll(baseline.Memory);

        Assert.IsFalse(baseline.FlyBird);
        Assert.IsNotNull(next.Result);
        Assert.IsNotNull(next.Memory);
        Assert.AreEqual(107, next.Result.Single().RepositoryId);
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("Repository 101 was not found or is inaccessible")));
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("No read permission for repository 102")));
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("Branch 'missing' does not exist in repository 103")));
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("Source file 'missing/en.po'") && warning.Contains("104")));
        Assert.IsTrue(fixture.Warnings.Any(warning => warning.Contains("Repository 106 was not found or is inaccessible")));
        Assert.IsFalse(fixture.RequestPaths.Any(path => path.StartsWith("/projects/105")));
        Assert.AreEqual(fixture.Now, next.Memory.Repositories[106].LastCheckedUtc);
        Assert.AreEqual(Baseline, next.Memory.Repositories[106].Watches.Single().LastCompletedScanUtc);
    }

    [TestMethod]
    public async Task PollReturnsOneFlightPerWatchWithOnlyTheCommitsThatEditedItsExactPath()
    {
        var fixture = new Fixture(Entry(101), Entry(101, mask: "alternate/[LOCALE].po"));
        fixture.Route = (_, path) => path switch
        {
            "/projects/101/repository/compare" => Json(new
            {
                commits = new[] { "edit-one", "unrelated", "edit-two", "created", "deleted", "renamed" }
                    .Select(id => new { id, message = id })
            }),
            "/projects/101/repository/commits/edit-one/diff" => Json(new[] { Diff("locales/en.po"), Diff("alternate/en.po") }),
            "/projects/101/repository/commits/edit-two/diff" => Json(new[] { Diff("locales/en.po") }),
            "/projects/101/repository/commits/unrelated/diff" => Json(new[] { Diff("locales/EN.po"), Diff("sub/locales/en.po") }),
            "/projects/101/repository/commits/created/diff" => Json(new[] { Diff("locales/en.po", added: true) }),
            "/projects/101/repository/commits/deleted/diff" => Json(new[] { Diff("locales/en.po", deleted: true) }),
            "/projects/101/repository/commits/renamed/diff" => Json(new[] { Diff("locales/en.po", renamed: true) }),
            _ => null
        };
        var baseline = await fixture.Poll();
        fixture.Now = Baseline.AddMinutes(1);

        var next = await fixture.Poll(baseline.Memory);

        Assert.IsTrue(next.FlyBird);
        Assert.IsNotNull(next.Result);
        Assert.AreEqual(2, next.Result.Count);
        CollectionAssert.AreEqual(new[] { "edit-one", "edit-two" }, next.Result.Single(result => result.FileMask == Mask)
            .Pushes.Single().Commits.Select(commit => commit.Hash).ToArray());
        Assert.AreEqual("edit-one", next.Result.Single(result => result.FileMask != Mask)
            .Pushes.Single().Commits.Single().Hash);
        Assert.AreEqual(1, fixture.RequestPaths.Count(path => path.EndsWith("/compare")));
        Assert.AreEqual(1, fixture.RequestPaths.Count(path => path.EndsWith("/edit-one/diff")));
        var method = typeof(ConfiguredSourcePollingList).GetMethod(nameof(ConfiguredSourcePollingList.OnSourceFilesModifiedAcrossRepos))!;
        Assert.IsTrue(method.IsDefined(typeof(MultipleEventsAttribute), false));
    }

    [TestMethod]
    public async Task FlightLimitStopsChecksAndOldestRepositoryResumesFromSerializedMemory()
    {
        var masks = Enumerable.Range(1, 21).Select(number => $"files/{number:D2}-[LOCALE].po").ToArray();
        var fixture = new Fixture([..masks.Select(mask => Entry(101, mask: mask)), Entry(202)]);
        fixture.Route = (_, path) => path == "/projects/101/repository/commits/edit/diff"
            ? Json(masks.Select(mask => Diff(mask.Replace("[LOCALE]", "en", StringComparison.Ordinal))))
            : null;
        var baseline = await fixture.Poll();
        var countBeforeFirstScan = fixture.RequestPaths.Count;
        fixture.Now = Baseline.AddMinutes(1);

        var first = await fixture.Poll(baseline.Memory);

        Assert.IsNotNull(first.Result);
        Assert.IsNotNull(first.Memory);
        Assert.AreEqual(20, first.Result.Count);
        Assert.IsTrue(first.Result.All(result => result.RepositoryId == 101));
        var firstRequests = fixture.RequestPaths.Skip(countBeforeFirstScan).ToList();
        Assert.IsFalse(firstRequests.Any(path => path.StartsWith("/projects/202")));
        Assert.IsFalse(firstRequests.Any(path => path.EndsWith("/21-en.po")));
        Assert.AreEqual(Baseline, first.Memory.Repositories[202].LastCheckedUtc);
        Assert.AreEqual(Baseline, first.Memory.Repositories[101].Watches.Single(watch => watch.FileMask == masks[^1]).LastCompletedScanUtc);
        var countBeforeSecondScan = fixture.RequestPaths.Count;
        fixture.Now = Baseline.AddMinutes(2);
        var persisted = JsonSerializer.Deserialize<ConfiguredSourcePollingMemory>(JsonSerializer.Serialize(first.Memory))!;

        var second = await fixture.Poll(persisted);

        Assert.IsNotNull(second.Result);
        CollectionAssert.AreEqual(new[] { 202, 101 }, second.Result.Select(result => result.RepositoryId).ToArray());
        Assert.AreEqual(masks[^1], second.Result[1].FileMask);
        CollectionAssert.AreEqual(new[] { "/projects/202", "/projects/101" }, fixture.RequestPaths
            .Skip(countBeforeSecondScan).Where(path => path is "/projects/101" or "/projects/202").ToArray());
        fixture.Now = Baseline.AddMinutes(3);
        Assert.IsFalse((await fixture.Poll(second.Memory)).FlyBird);
    }

    [TestMethod]
    public async Task MetadataUploadFailureFailsTheCallAndLeavesThePreviousPollRetryable()
    {
        var fixture = new Fixture(Entry(101));
        var baseline = await fixture.Poll();
        fixture.Now = Baseline.AddMinutes(1);
        fixture.Files.FailUpload = true;
        var beforeFailure = JsonSerializer.Serialize(baseline.Memory);

        await Assert.ThrowsExactlyAsync<IOException>(() => fixture.Poll(baseline.Memory));
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            fixture.Actions.Search(ConfigRepository, ConfigBranch, ConfigFile, new()));
        Assert.AreEqual(beforeFailure, JsonSerializer.Serialize(baseline.Memory));

        fixture.Files.FailUpload = false;
        var retried = await fixture.Poll(baseline.Memory);
        Assert.IsNotNull(retried.Result);
        Assert.AreEqual(101, retried.Result.Single().RepositoryId);
    }

    [TestMethod]
    public async Task ARepositoryAccessFailurePartwayThroughAWatchKeepsEarlierPushesRetryable()
    {
        var fixture = new Fixture(Entry(101), Entry(202));
        var unavailable = true;
        fixture.Route = (_, path) => path switch
        {
            "/projects/101/events" => Json(new[] { Push(101, 20, "main"), Push(101, 10, "main") }),
            "/projects/101/repository/compare" => fixture.CompareRequestCount % 2 == 0 && unavailable
                ? GitLabTestData.Json("{}", HttpStatusCode.Forbidden)
                : Json(new { commits = new[] { new { id = "edit" } } }),
            _ => null
        };
        var baseline = await fixture.Poll();
        fixture.Now = Baseline.AddMinutes(1);

        var first = await fixture.Poll(baseline.Memory);

        Assert.IsNotNull(first.Result);
        Assert.IsNotNull(first.Memory);
        Assert.AreEqual(202, first.Result.Single().RepositoryId);
        Assert.AreEqual(0, first.Memory.Repositories[101].Watches.Single().RecentEvents.Count);
        unavailable = false;
        fixture.Now = Baseline.AddMinutes(2);
        var retry = await fixture.Poll(first.Memory);
        Assert.IsNotNull(retry.Result);
        Assert.AreEqual(101, retry.Result.Single().RepositoryId);
        Assert.AreEqual(2, retry.Result.Single().Pushes.Count);
    }

    [TestMethod]
    public async Task ReactivationAndRetargetingSetANewBaselineAndMetadataEditsPreserveProgress()
    {
        var fixture = new Fixture(Entry(101), Entry(202), Entry(303));
        var baseline = await fixture.Poll();
        fixture.Now = Baseline.AddMinutes(1);
        fixture.Entries[0]!["metadata"] = new JsonObject { ["edited"] = true };
        fixture.Entries[1]!["enabled"] = false;
        fixture.Entries[2]!["sourceLocale"] = "en_US";

        var changed = await fixture.Poll(baseline.Memory);

        Assert.IsNotNull(changed.Result);
        Assert.IsNotNull(changed.Memory);
        Assert.AreEqual(101, changed.Result.Single().RepositoryId);
        Assert.IsFalse(changed.Memory.Repositories.ContainsKey(202));
        Assert.AreEqual(fixture.Now, changed.Memory.Repositories[303].Watches.Single().ActiveSinceUtc);
        Assert.AreEqual(Baseline, changed.Memory.Repositories[101].Watches.Single().ActiveSinceUtc);
        fixture.Now = Baseline.AddMinutes(2);
        fixture.Entries[1]!["enabled"] = true;
        var reenabled = await fixture.Poll(changed.Memory);
        Assert.IsFalse(reenabled.FlyBird);
        Assert.IsNotNull(reenabled.Memory);
        Assert.AreEqual(fixture.Now, reenabled.Memory.Repositories[202].Watches.Single().ActiveSinceUtc);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GitLabSandboxReturnsConfigurationsAndMatchingPushesAcrossRepositoriesAndBranches()
    {
        var token = Environment.GetEnvironmentVariable("GITLAB_CONFIG_SANDBOX_TOKEN");
        var repositoryId = Environment.GetEnvironmentVariable("GITLAB_CONFIG_SANDBOX_REPOSITORY");
        var baselineTime = Environment.GetEnvironmentVariable("GITLAB_CONFIG_SANDBOX_BASELINE");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(repositoryId) ||
            string.IsNullOrWhiteSpace(baselineTime))
            Assert.Inconclusive("Set the GitLab configuration sandbox variables for this test.");

        AuthenticationCredentialsProvider[] credentials =
        [
            new(CredNames.ConnectionType, ConnectionTypes.PersonalAccessToken),
            new(CredNames.Authorization, token!)
        ];
        var warnings = new List<string>();
        var context = new InvocationContext { AuthenticationCredentialsProviders = credentials, Logger = CreateLogger(warnings) };
        var client = new BlackbirdGitlabClient(credentials);
        var files = new CapturedFiles();
        var actions = new RepositoryConfigurationActions(context, files);
        var repository = new GetRepositoryRequest { RepositoryId = repositoryId! };
        var file = new ConfigurationFileInput { FilePath = "blackbird-config.json" };
        var picker = new ConfigurationFilePicker(context, repository, new GetBranchRequest { Name = "secondary" });
        var filesOnBranch = await picker.GetFolderContentAsync(new(), CancellationToken.None);
        Assert.IsTrue(filesOnBranch.Any(item => item.Id == file.FilePath));
        var get = await actions.Get(repository, ConfigBranch, file,
            new RepositoryConfigurationInput { RepositoryId = repositoryId!, Branch = "secondary", FileMask = Mask });
        Assert.AreEqual("secondary", get.Branch);
        var search = await actions.Search(repository, ConfigBranch, file, new() { TargetLocales = ["fr"] });
        Assert.AreEqual(2, search.Configurations.Count);
        var nested = await actions.Search(repository, ConfigBranch, file, new() { ContainsMetadata = "Sandbox alternate" });
        Assert.AreEqual("locales/en/lc_messages/messages.po", nested.Configurations.Single().SourceFilePath);

        var now = DateTimeOffset.Parse(baselineTime!, CultureInfo.InvariantCulture);
        var poller = new ConfiguredSourcePollingList(context, files, client, () => now);
        var first = await poller.OnSourceFilesModifiedAcrossRepos(new(), repository, ConfigBranch, file);
        Assert.IsFalse(first.FlyBird);
        now = DateTimeOffset.UtcNow;
        var second = await poller.OnSourceFilesModifiedAcrossRepos(new() { Memory = first.Memory }, repository, ConfigBranch, file);
        Assert.IsTrue(second.FlyBird, string.Join(Environment.NewLine, warnings));
        Assert.IsNotNull(second.Result);
        Assert.AreEqual(4, second.Result.Count);
        Assert.AreEqual(2, second.Result.Select(result => result.RepositoryId).Distinct().Count());
        Assert.AreEqual(2, second.Result.Select(result => result.Branch).Distinct().Count());
        Assert.IsTrue(second.Result.All(result => result.Pushes.Single().Commits.Count() == 1));
        var third = await poller.OnSourceFilesModifiedAcrossRepos(new() { Memory = second.Memory }, repository, ConfigBranch, file);
        Assert.IsFalse(third.FlyBird);
    }

    private static Logger CreateLogger(List<string> warnings) => new("Debug")
    {
        LogWarning = (message, arguments) => warnings.Add(string.Format(CultureInfo.InvariantCulture,
            message ?? string.Empty, (arguments ?? []).Cast<object>().ToArray()))
    };

    private static JsonObject Entry(int repositoryId, string branch = "main", string mask = Mask, bool enabled = true)
        => new()
        {
            ["repositoryId"] = repositoryId,
            ["branch"] = branch,
            ["fileMask"] = mask,
            ["sourceLocale"] = "en",
            ["targetLocales"] = new JsonArray("fr", "pt_BR"),
            ["enabled"] = enabled,
            ["metadata"] = JsonNode.Parse("""{"workflow":[{"department":"Localization Team","priority":3}]}""")
        };

    private static object Push(int projectId, long eventId, string branch)
        => new
        {
            id = eventId, project_id = projectId, created_at = PushTime, author_id = 70,
            push_data = new { commit_count = 1, action = "pushed", ref_type = "branch", @ref = branch,
                commit_from = "before", commit_to = $"after-{eventId}" }
        };

    private static object Diff(string path, bool added = false, bool deleted = false, bool renamed = false)
        => new { new_path = path, old_path = path, new_file = added, deleted_file = deleted, renamed_file = renamed };

    private static HttpResponseMessage Json<T>(T value) => GitLabTestData.Json(JsonSerializer.Serialize(value));

    private sealed class Fixture
    {
        private readonly StubHttpMessageHandler _handler;
        private readonly InvocationContext _context;

        internal Fixture(params JsonNode[] entries)
        {
            Entries = new JsonArray(entries);
            _context = GitLabTestData.CreateContext();
            _context.Logger = CreateLogger(Warnings);
            _handler = new StubHttpMessageHandler((request, _) => Task.FromResult(Respond(request)));
        }

        internal JsonArray Entries { get; }
        internal List<string> Warnings { get; } = [];
        internal CapturedFiles Files { get; } = new();
        internal DateTimeOffset Now { get; set; } = Baseline;
        internal Func<HttpMethod, string, HttpResponseMessage?>? Route { get; set; }
        internal HttpResponseMessage? ConfigurationOverride { get; init; }
        internal List<string> RequestPaths => _handler.RequestUris.Select(PathFor).ToList();
        internal int CompareRequestCount => RequestPaths.Count(path => path.EndsWith("/compare"));

        internal RepositoryConfigurationActions Actions => new(_context, Files)
        {
            ClientOverride = GitLabTestData.CreateClient(_handler)
        };

        internal Task<PollingEventResponse<ConfiguredSourcePollingMemory, List<ConfiguredSourcePollingOutput>>> Poll(
            ConfiguredSourcePollingMemory? memory = null)
            => new ConfiguredSourcePollingList(_context, Files, GitLabTestData.CreateClient(_handler), () => Now)
                .OnSourceFilesModifiedAcrossRepos(new() { Memory = memory }, ConfigRepository, ConfigBranch, ConfigFile);

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = PathFor(request.RequestUri!);
            if (path == "/projects/900/repository/files/settings/blackbird.json/raw")
                return ConfigurationOverride ?? GitLabTestData.Json(Entries.ToJsonString());

            var overridden = Route?.Invoke(request.Method, path);
            if (overridden is not null)
                return overridden;

            var segments = path.Split('/');
            var projectId = int.Parse(segments[2], CultureInfo.InvariantCulture);
            return path switch
            {
                _ when segments.Length == 3 => Json(new { path_with_namespace = $"team/{projectId}" }),
                _ when path.Contains("/branches/") || request.Method == HttpMethod.Head => Json(new { }),
                _ when path.EndsWith("/events") => Json(new[] { Push(projectId, 10, "main") }),
                _ when path.EndsWith("/compare") => Json(new { commits = new[] { new { id = "edit", message = "change" } } }),
                _ when path.EndsWith("/diff") => Json(new[] { Diff("locales/en.po") }),
                _ => throw new InvalidOperationException($"Unexpected request {request.Method} {path}")
            };
        }

        private static string PathFor(Uri uri) => Uri.UnescapeDataString(uri.AbsolutePath["/api/v4".Length..]);
    }

    private sealed class CapturedFiles : IFileManagementClient
    {
        internal Dictionary<string, (string ContentType, string Text)> Uploads { get; } = new();
        internal bool FailUpload { get; set; }

        public async Task<FileReference> UploadAsync(Stream stream, string contentType, string fileName)
        {
            if (FailUpload)
                throw new IOException("Metadata storage is unavailable.");

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            Uploads[fileName] = (contentType, await reader.ReadToEndAsync());
            return new FileReference { Name = fileName, ContentType = contentType };
        }

        public Task<Stream> DownloadAsync(FileReference reference) => throw new NotSupportedException();
    }
}
