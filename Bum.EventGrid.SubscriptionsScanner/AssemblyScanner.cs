

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CSharpFunctionalExtensions;
using Newtonsoft.Json;
using Serilog;
using superfeed.eventgrid.annotations;

namespace superfeed.eventgrid.tool
{
    public static class AssemblyScanner
    {
        public static Task<Result<ImmutableList<SubscriptionAttribute>>> ScanSubscriptions(string solutionPath, string? filter) =>
            Directory
                .GetFiles(solutionPath, "function.json", SearchOption.AllDirectories)
                .Where(path => !path.Contains("output"))
                .Where(path => !path.Contains("api.metadata"))
                .Select(LocateAssemblyPathOrEmpty)
                .Combine()
                .Map(p => p.Where(p => p.Exists()))
                .Map(p => p.Where(s => filter switch
                {
                    null => true,
                    _ => Regex.IsMatch(s, FilteringRegex(filter))
                }))
                .Map(f => f.Distinct().ToImmutableList())
                .Tap(paths => Log.Logger.Information($"Found {paths.Count()} assemblies:\n {string.Join("\n", paths)}"))
                .Map(ScanAssemblies);

        private static async Task<Result<string>> LocateAssemblyPathOrEmpty(string path)
        {
            var context = new AssemblyLoadContext("", true);
            string content = await File.ReadAllTextAsync(path);
            FunctionJson? deserialized = JsonConvert.DeserializeObject<FunctionJson>(content);
            if(deserialized.ScriptFile?.DoesNotExist() ?? true)
                return string.Empty;
            var result = Path.GetFullPath(deserialized.ScriptFile, Directory.GetParent(path).FullName);
            return result;
        }

        private static ImmutableList<SubscriptionAttribute> ScanAssemblies(ImmutableList<string> paths)
        {
            var context = new AssemblyLoadContext("tmp", true);
            var subs = paths
                .Select(context.LoadFromAssemblyPath)
                .ToImmutableList()
                .Select(ScanSubscriptions)
                .SelectMany(s => s)
                .ToImmutableList();
            context.Unload();
            return subs;
        }
            

        private static ImmutableList<SubscriptionAttribute> ScanSubscriptions(Assembly assembly)
        {
            Log.Logger.Information($"Scanning [{assembly.FullName}]...");
            ImmutableList<Result<ImmutableList<SubscriptionAttribute>>> attributes = assembly
                .GetExportedTypes()
                .SelectMany(t => t.GetMethods())
                .Select(SafeGetMethod)
                .ToImmutableList();

            return Result.Success(attributes
                .Where(a => a.IsSuccess)
                .SelectMany(a => a.Value)
                .ToImmutableList())
                .Tap(s => Log.Logger.Information($"Found {s.Count} subscriptions"))
                .Value;
        }

        private static Result<ImmutableList<SubscriptionAttribute>> SafeGetMethod(MethodInfo info)
        {
            try
            {
                var attrs = info.GetCustomAttributes()
                    .OfType<SubscriptionAttribute>()
                    .ToImmutableList();
                return Result.Success(attrs);
            }
            catch (Exception e)
            {
                return Result.Failure<ImmutableList<SubscriptionAttribute>>($"Failed for method {info.Name}");
            }
        }
        private static string FilteringRegex(String value) {
            return Regex.Escape(value).Replace("\\*", ".*") + "$"; 
        }
    }

    public class FunctionJson
    {
        public string? ScriptFile { get; set; }
        public string? EntryPoint { get; set; }
    }
    
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector)
        {
            var keysHasSet = new HashSet<TKey>();
            foreach (TSource sourceElement in source)
            {
                TKey currentKey = selector(sourceElement);

                if (keysHasSet.Add(currentKey))
                    yield return sourceElement;
            }
        }
        public static List<List<T>> Split<T>(this IEnumerable<T> enumerable, int size)
        {
            var list = enumerable.ToList();
            var chunks = new List<List<T>>();
            int chunkCount = list.Count / size;

            if (list.Count % size > 0)
            {
                chunkCount++;
            }

            for (var chunkNumber = 0; chunkNumber < chunkCount; chunkNumber++)
            {
                chunks.Add(list.Skip(chunkNumber * size).Take(size).ToList());
            }

            return chunks;
        }

        public static bool None<T>(this IEnumerable<T> enumerable) => !enumerable.Any();
        public static bool NullOrNone<T>(this IEnumerable<T> enumerable) => enumerable == null || enumerable.None();
    }
    
     public static class StringExtensions
    {
        public static string GetRandomString(int length)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            IEnumerable<char> randomChars = Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]);
            return new string(randomChars.ToArray()).ToLower();
        }
        public static bool IsQuoted(this string s) => s.StartsWith("\"") && s.EndsWith("\"");
        public static string ToBase64(this string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        public static string FromBase64(this string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));
        public static bool IsBase64Image(this string s)
        {
            if (s.DoesNotExist())
                return false;

            return Regex.Match(s, "^data:image\\/(?<format>[a-zA-Z]+);base64,").Success;
        }

        public static bool ContainsAny(this string str, params string[] values)
            => values.Any(str.Contains);
        public static string RemoveHtmlTags(this string str)
        {
            str = Regex.Replace(str, @"<[^>]*>", string.Empty);
            return HtmlDecode(str);
        }

        public static string UseNewIfNotEmpty(this string original, string other)
            => other.Exists() ? other : original;

        public static string IfEmpty(this string original, string other)
            => original.DoesNotExist() ? other : original;
        public static bool Exists(this string str) => !string.IsNullOrEmpty(str);

        public static bool DoesNotExist(this string str) => string.IsNullOrWhiteSpace(str);
        public static string HtmlDecode(this string str) => HttpUtility.HtmlDecode(str);
        public static string Partition(this string str) => str.Split('-')[0];

        public static string Primary(this string str) => str;

        public static bool IsValidUrl(this string str) =>
            Uri.TryCreate(str, UriKind.Absolute, out Uri validUri) &&
            (validUri.Scheme == Uri.UriSchemeHttp || validUri.Scheme == Uri.UriSchemeHttps);

        public static string ReplaceFirstOccurenceOrOriginal(this string originalText, string oldValue, string newValue)
        {
            if (originalText.DoesNotExist() || oldValue.DoesNotExist() || newValue.DoesNotExist())
            {
                return originalText;
            }

            int oldValueFirstOccurenceStartIndex = originalText.IndexOf(oldValue, StringComparison.Ordinal);
            if (oldValueFirstOccurenceStartIndex < 0)
            {
                return originalText;
            }

            int oldValueFirstOccurenceEndIndex = oldValueFirstOccurenceStartIndex + oldValue.Length;
            return new StringBuilder()
                .Append(originalText.Substring(0, oldValueFirstOccurenceStartIndex))
                .Append(newValue)
                .Append(originalText.Substring(oldValueFirstOccurenceEndIndex))
                .ToString();
        }

        public static string Truncate(this string originalText, int maxLength)
        {
            if (maxLength <= 0)
            {
                return string.Empty;
            }

            if (originalText.DoesNotExist())
            {
                return originalText;
            }

            return originalText.Length <= maxLength ? originalText : originalText.Substring(0, maxLength);
        }

        public static bool ExistsAndEquals(this string input, string compareWith) =>
            input.Exists() && input.Equals(compareWith);

        public const string Dash = "-";
        public static string Prefix(this string s, string prefix, string separator = Dash) => $"{prefix}{separator}{s}";
        public static string Suffix(this string s, string suffix, string separator = "") => $"{s}{separator}{suffix}";
        public static string DashSuffix(this string s, string suffix) => s.Suffix(suffix, Dash);
    }
}