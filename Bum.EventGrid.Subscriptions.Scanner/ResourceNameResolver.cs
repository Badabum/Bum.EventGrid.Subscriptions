using System.Text.RegularExpressions;

namespace Bum.EventGrid.Subscriptions.Scanner
{
    public static class ResourceNameResolver
    {
        public const string EnvironmentToken = "environment";
        public static string Resolve(string resourceName, string env)
        {
            Regex replaceRegex = new Regex(@"(?<replaceGroup>{(?<replacementKey>[a-zA-Z_-]*)})");
            Match regexMatch = replaceRegex.Match(resourceName);
            string placeholder = regexMatch
                .Groups["replaceGroup"]
                .Value;
            string placeholderKey = regexMatch
                .Groups["replacementKey"]
                .Value;
            if (placeholderKey == EnvironmentToken)
                return resourceName.Replace(placeholder, env);
            return resourceName;
        }
    }
}