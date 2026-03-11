using Apps.Gitlab.Connections;
using Apps.GitLab.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Tests.GitLab.Base;

namespace Tests.GitLab;

[TestClass]
public class ConnectionValidatorTests : TestBaseWithContext
{
    [TestMethod, ContextDataSource(ConnectionTypes.OAuth)]
    public async Task ValidateConnection_WithCorrectCredentials_ReturnsValidResult(InvocationContext context)
    {
        var validator = new ConnectionValidator();

        var tasks = CredentialGroups.Select(x => validator.ValidateConnection(x, CancellationToken.None).AsTask());
        var results = await Task.WhenAll(tasks);
        Assert.IsTrue(results.All(x => x.IsValid));
    }

    [TestMethod, ContextDataSource]
    public async Task ValidateConnection_WithIncorrectCredentials_ReturnsInvalidResult(InvocationContext context)
    {
        var validator = new ConnectionValidator();

        var newCreds = context.AuthenticationCredentialsProviders
            .Select(x =>
            {
                if (x.KeyName == "Authorization" || x.KeyName == "API key" || x.KeyName == "access_token")
                    return new AuthenticationCredentialsProvider(x.KeyName, x.Value + "_incorrect");

                return new AuthenticationCredentialsProvider(x.KeyName, x.Value);
            });

        var result = await validator.ValidateConnection(newCreds, CancellationToken.None);

        Assert.IsFalse(result.IsValid);
    }
}