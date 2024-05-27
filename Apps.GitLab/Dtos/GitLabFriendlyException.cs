using Newtonsoft.Json.Linq;

namespace Apps.GitLab.Dtos;

public class GitLabFriendlyException : ArgumentException
{
    public GitLabFriendlyException(string message) : base(ParseError(message))
    {
    }

    private static string ParseError(string message)
    {
        try
        {
            var errorObj = JObject.Parse(message);
            var messageValue = errorObj.GetValue("message");
            if (messageValue is JArray)
            {
                var messages = messageValue.ToArray();
                return string.Join(", ", messages.Select(x => x.ToString()));
            }
            else if (messageValue is JValue)
            {
                return messageValue.ToString();
            }
        }
        catch {}
        return message;
    }
}