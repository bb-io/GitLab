using Blackbird.Applications.Sdk.Common;

namespace Apps.Gitlab.Models.User.Requests;

public class UserDataRequest
{
    [Display("Username")]
    public string Username { get; set; }
}