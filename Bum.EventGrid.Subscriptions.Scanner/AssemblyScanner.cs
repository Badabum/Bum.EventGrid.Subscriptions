using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Bum.EventGrid.Subscriptions.Annotations;
using CSharpFunctionalExtensions;
using Serilog;

namespace Bum.EventGrid.Subscriptions.Scanner
{
    public static class AssemblyScanner
    {
        public static Result<ImmutableList<SubscriptionAttribute>> ScanSubscriptions(string assemblyPath) =>
            ScanAssemblies(assemblyPath);

        private static ImmutableList<SubscriptionAttribute> ScanAssemblies(string assemblyPath)
        {
            var context = new AssemblyLoadContext("tmp", true);
            context.Resolving += (ctx, name) =>
            {
                string path = @$"{assemblyPath.Replace(Path.GetFileName(assemblyPath), String.Empty)}\{name.Name}.dll";
                return context.LoadFromAssemblyPath(path);
            };
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var subs = ScanSubscriptions(assembly);
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
    }
}