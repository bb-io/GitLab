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
    public async Task ValidateConnectionOAuth_WithCorrectCredentials_ReturnsValidResult(InvocationContext context)
    {

        var validator = new ConnectionValidator();

        var result = await validator.ValidateConnection(
            context.AuthenticationCredentialsProviders,
            CancellationToken.None);

        Assert.IsTrue(result.IsValid, result.Message);
    }

    [TestMethod, ContextDataSource(ConnectionTypes.PersonalAccessToken)]
    public async Task ValidateConnectionPAT_WithCorrectCredentials_ReturnsValidResult(InvocationContext context)
    {

        var validator = new ConnectionValidator();

        var result = await validator.ValidateConnection(
            context.AuthenticationCredentialsProviders,
            CancellationToken.None);

        Assert.IsTrue(result.IsValid, result.Message);
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