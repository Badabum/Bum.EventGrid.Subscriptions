using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Bum.EventGrid.Subscriptions.Annotations;
using CommandLine;
using CSharpFunctionalExtensions;
using Microsoft.Azure.Management.EventGrid;
using Microsoft.Azure.Management.EventGrid.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using Polly;
using Serilog;

namespace Bum.EventGrid.Subscriptions.Scanner
{
    public class Options
    {
        [Option('t', "targetAssembly", Required = true, HelpText = "Target assembly path")] public string TargetAssembly { get; set; }

        [Option('s', "subscriptionId", Required = true, HelpText = "Azure subscription identifier")]
        public string Subscription { get; set; }

        [Option('e', "environment", Required = true, HelpText = "If you're using different environments in azure pass the name of environment here")]
        public string Environment { get; set; }

        [Option("dry-run", Required = false, HelpText = "Prints out all discovered subscriptions")]
        public bool DryRun { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            await Parser.Default
                .ParseArguments<Options>(args)
                .WithParsedAsync(opts => UpdateSubscriptions(opts)
                    .Tap(subs => Log.Logger.Information(JsonConvert.SerializeObject(subs, new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented
                    }))));
        }
        
        private static async Task<Result<ImmutableList<EventSubscription>>> UpdateSubscriptions(Options options)
        {
            AccessToken defaultToken = await new DefaultAzureCredential(new DefaultAzureCredentialOptions()
                {
                    ExcludeVisualStudioCodeCredential = true,
                    ExcludeEnvironmentCredential = true,
                    ExcludeInteractiveBrowserCredential = true,
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeVisualStudioCredential = true
                })
                .GetTokenAsync(new TokenRequestContext(new[] {"https://management.azure.com"}));
            var token = new TokenCredentials(defaultToken.Token);
            var client = new EventGridManagementClient(token);
            return await AssemblyScanner
                .ScanSubscriptions(options.TargetAssembly)
                .Tap(subs => Log.Logger.Information(JsonConvert.SerializeObject(subs, new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented
                })))
                .Bind(subscriptions =>
                    options.DryRun
                        ? Task.FromResult(
                            Result.Success<ImmutableList<EventSubscription>>(ImmutableList<EventSubscription>.Empty))
                        : ApplySubscriptions(client, options.Subscription, options.Environment, subscriptions));
        }
        private static Task<Result<ImmutableList<EventSubscription>>> ApplySubscriptions(
            EventGridManagementClient client,
            string subscriptionId,
            string env,
            ImmutableList<SubscriptionAttribute> attributes)
            => attributes.Select(s => s switch
                {
                    FunctionAppSubscriptionAttribute func => CreateFunctionAppSubscription(client, func, subscriptionId,
                        env),
                    WebhookSubscriptionAttribute hook => CreateWebhookSubscription(client, hook, subscriptionId, env)
                })
                .Combine()
                .Map(l => l.ToImmutableList());
        private static Task<Result<EventSubscription>> CreateWebhookSubscription(
            EventGridManagementClient client,
            WebhookSubscriptionAttribute spec,
            string subscriptionId,
            string environment
        )
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(BackoffStrategy.DecorrelatedJitterBackoff(
                    TimeSpan.FromSeconds(5), 
                    TimeSpan.FromSeconds(30), 4, fastFirst: true))
                .ExecuteAsync(async () =>
                {
                    EventSubscription subscription = await client.EventSubscriptions.CreateOrUpdateAsync(
                        ResourceMatcher.GetResourceId(
                            spec.ResourceType,
                            ResourceNameResolver.Resolve(spec.ResourceName, environment),
                            ResourceNameResolver.Resolve("feedme-{environment}", environment),
                            subscriptionId),
                        spec.Name, new EventSubscription()
                        {
                            Destination = new WebHookEventSubscriptionDestination()
                            {
                                EndpointUrl = spec.Endpoint
                            },
                            Filter = new EventSubscriptionFilter()
                            {
                                SubjectBeginsWith = spec.Subject,
                                IncludedEventTypes = spec.EventTypes
                            }
                        });
                    return Result.Success(subscription);
                });
        }

        private static async Task<Result<EventSubscription>> CreateFunctionAppSubscription(
            EventGridManagementClient client,
            FunctionAppSubscriptionAttribute spec,
            string subscriptionId,
            string environment
            )
        {
            EventSubscription subscription = await client.EventSubscriptions.CreateOrUpdateAsync(
                ResourceMatcher.GetResourceId(
                    spec.ResourceType, 
                    ResourceNameResolver.Resolve(spec.ResourceName, environment), 
                    ResourceNameResolver.Resolve("feedme-{environment}", environment), 
                    subscriptionId), 
                spec.Name, new EventSubscription()
                {
                    Destination = new AzureFunctionEventSubscriptionDestination()
                    {
                        ResourceId = ResourceMatcher.GetFunctionAppId(
                            subscriptionId,
                            ResourceNameResolver.Resolve("feedme-{environment}", environment), 
                            ResourceNameResolver.Resolve(spec.FunctionAppName, environment),
                            ResourceNameResolver.Resolve(spec.FunctionName, environment))
                    },
                    Filter = new EventSubscriptionFilter()
                    {
                        SubjectBeginsWith = spec.Subject,
                        IncludedEventTypes = spec.EventTypes
                    },
                    Labels = new List<string>(){ "autogenerated" }
                });
            return Result.Success(subscription);
        }
    }
}