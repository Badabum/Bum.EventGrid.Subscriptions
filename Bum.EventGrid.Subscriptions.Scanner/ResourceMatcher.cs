using Bum.EventGrid.Subscriptions.Annotations;

namespace Bum.EventGrid.Subscriptions.Scanner
{
    public static class ResourceMatcher
    {
        public static string GetFunctionAppId(
            string subscriptionId,
            string resourceGroup,
            string functionAppName,
            string functionName
        )
            => $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{functionAppName}/functions/{functionName}";
        public static string GetResourceId(
            EventResourceType type,
            string resourceName,
            string resourceGroup,
            string subscriptionId)
            =>
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{MatchResourceProvider(type)}/{MatchResourceType(type)}/{resourceName}";
        private static string MatchResourceProvider(EventResourceType type)
            => type switch
            {
                EventResourceType.StorageAccount => "Microsoft.Storage",
                EventResourceType.EventGridTopic => "Microsoft.EventGrid"
            };

        private static string MatchResourceType(EventResourceType type)
            => type switch
            {
                EventResourceType.StorageAccount => "StorageAccounts",
                EventResourceType.EventGridTopic => "topics"
            };
    }
}