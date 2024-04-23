using Apps.Gitlab.Webhooks.Payloads;
using Apps.GitLab.Webhooks.Handlers;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using System.Net;

namespace Apps.Gitlab.Webhooks;

[WebhookList]
public class PushWebhooks
{
    [Webhook("On commit pushed", typeof(PushEventHandler), Description = "On commit pushed")]
    public async Task<WebhookResponse<PushPayload>> CommitPushedHandler(WebhookRequest webhookRequest)
    {
        var data = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body.ToString());
        if (data is null) { throw new InvalidCastException(nameof(webhookRequest.Body)); }
        
        return new WebhookResponse<PushPayload>
        {
            HttpResponseMessage = null,
            Result = data
        };
    }

    [Webhook("On files added", typeof(PushEventHandler), Description = "On files added")]
    public async Task<WebhookResponse<FilesListResponse>> FilesAddedHandler(WebhookRequest webhookRequest,
        [WebhookParameter] FolderInput input)
    {
        var data = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body.ToString());
        if (data is null) { throw new InvalidCastException(nameof(webhookRequest.Body)); }

        var addedFiles = new List<FilePathObj>();
        data.Commits.ForEach(c => addedFiles.AddRange(c.Added.Where(f => input.FolderPath is null || IsFilePathMatchingPattern(input.FolderPath, f))
            .Select(filePath => new FilePathObj { FilePath = filePath })));
        if (addedFiles.Any())
        {
            return new WebhookResponse<FilesListResponse>
            {
                HttpResponseMessage = null,
                Result = new FilesListResponse
                {
                    Files = addedFiles,
                }
            };
        }
        return GeneratePreflight<FilesListResponse>();
    }

    [Webhook("On files modified", typeof(PushEventHandler), Description = "On files modified")]
    public async Task<WebhookResponse<FilesListResponse>> FilesModifiedHandler(WebhookRequest webhookRequest,
        [WebhookParameter] FolderInput input)
    {
        var data = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body.ToString());
        if (data is null) { throw new InvalidCastException(nameof(webhookRequest.Body)); }
        
        var modifiedFiles = new List<FilePathObj>();
        data.Commits.ForEach(c => modifiedFiles.AddRange(c.Modified.Where(f => input.FolderPath is null || IsFilePathMatchingPattern(input.FolderPath, f))
            .Select(filePath => new FilePathObj { FilePath = filePath })));
        if (modifiedFiles.Any())
        {
            return new WebhookResponse<FilesListResponse>
            {
                HttpResponseMessage = null,
                Result = new FilesListResponse
                {
                    Files = modifiedFiles
                }
            };
        }
        return GeneratePreflight<FilesListResponse>();
    }

    [Webhook("On files added or modified", typeof(PushEventHandler), Description = "On files added or modified")]
    public async Task<WebhookResponse<FilesListResponse>> FilesAddedAndModifiedHandler(WebhookRequest webhookRequest,
        [WebhookParameter] FolderInput input)
    {
        var data = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body.ToString());
        if (data is null) { throw new InvalidCastException(nameof(webhookRequest.Body)); }
        
        var files = new List<FilePathObj>();
        data.Commits.ForEach(c =>
        {
            files.AddRange(c.Added.Where(f => input.FolderPath is null || IsFilePathMatchingPattern(input.FolderPath, f))
                .Select(fileId => new FilePathObj { FilePath = fileId }));
            files.AddRange(c.Modified.Where(f => input.FolderPath is null || IsFilePathMatchingPattern(input.FolderPath, f))
                .Select(fileId => new FilePathObj { FilePath = fileId }));
        });
        if (files.Any())
        {
            return new WebhookResponse<FilesListResponse>
            {
                HttpResponseMessage = null,
                Result = new FilesListResponse
                {
                    Files = files,
                }
            };
        }
        return GeneratePreflight<FilesListResponse>();
    }

    [Webhook("On files removed", typeof(PushEventHandler), Description = "On files removed")]
    public async Task<WebhookResponse<FilesListResponse>> FilesRemovedHandler(WebhookRequest webhookRequest,
        [WebhookParameter] FolderInput input)
    {
        var data = JsonConvert.DeserializeObject<PushPayload>(webhookRequest.Body.ToString());
        if (data is null) { throw new InvalidCastException(nameof(webhookRequest.Body)); }
        
        var removedFiles = new List<FilePathObj>();
        data.Commits.ForEach(c => removedFiles.AddRange(c.Removed.Where(f => input.FolderPath is null || IsFilePathMatchingPattern(input.FolderPath, f))
            .Select(filePath => new FilePathObj { FilePath = filePath })));
        if (removedFiles.Any())
        {
            return new WebhookResponse<FilesListResponse>
            {
                HttpResponseMessage = null,
                Result = new FilesListResponse
                {
                    Files = removedFiles
                }
            };
        }
        return GeneratePreflight<FilesListResponse>();
    }
    public static bool IsFilePathMatchingPattern(string pattern, string filePath)
    {
        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        return matcher.Match(filePath).HasMatches;
    }

    private WebhookResponse<T> GeneratePreflight<T>() where T : class
    {
        return new WebhookResponse<T>
        {
            ReceivedWebhookRequestType = WebhookRequestType.Preflight,
            HttpResponseMessage = new HttpResponseMessage(statusCode: HttpStatusCode.OK)
        };
    }
}