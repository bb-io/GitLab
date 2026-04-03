using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.GitLab.Utils;

public static class ParsingUtils
{
    public static int ParseIntOrThrow(string value, string fieldName)
    {
        if (int.TryParse(value, out var parsed))
            return parsed;

        throw new PluginMisconfigurationException($"{fieldName} should be a valid integer.");
    }
}
