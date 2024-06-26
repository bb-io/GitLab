﻿using Apps.Gitlab.Webhooks.Payloads;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Apps.Gitlab.Webhooks.Bridge;

public class BridgeService
{
    private string BridgeServiceUrl { get; set; }
    public BridgeService(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders, string bridgeServiceUrl) 
    {
        BridgeServiceUrl = bridgeServiceUrl;
    }
    public void Subscribe(string _event, string repositoryId, string url)
    {
        var client = new RestClient(BridgeServiceUrl);
        var request = new RestRequest($"/{repositoryId}/{_event}", Method.Post);
        //request.AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);
        request.AddBody(url);
        client.Execute(request);
    }

    public void Unsubscribe(string _event, string repositoryId, string url)
    {
        var client = new RestClient(BridgeServiceUrl);
        var requestGet = new RestRequest($"/{repositoryId}/{_event}", Method.Get);
        //requestGet.AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);
        var webhooks = client.Get<List<BridgeGetResponse>>(requestGet);
        var webhook = webhooks.FirstOrDefault(w => w.Value == url);

        var requestDelete = new RestRequest($"/{repositoryId}/{_event}/{webhook.Id}", Method.Delete);
        //requestDelete.AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);
        client.Delete(requestDelete);
    }
}