using Newtonsoft.Json.Linq;

namespace Apps.GitLab.Dtos;

public class GitLabFriendlyException : Exception
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

            if (messageValue is JArray messageArr)
                return string.Join(", ", messageArr.Select(x => x.ToString()));

            if (messageValue is JValue)
                return messageValue.ToString();

            if (messageValue is JObject)
            {
                var messageChildren = messageValue
                    .Children()
                    .Where(x => x is JProperty { Value: JArray })
                    .OfType<JProperty>()
                    .Select(x => $"{x.Name} - {string.Join(';', x.Value.Select(x => x.ToString()))}");

                return string.Join(Environment.NewLine, messageChildren);
            }
        }
        catch
        {
            // ignored
        }

        return message;
    }
}