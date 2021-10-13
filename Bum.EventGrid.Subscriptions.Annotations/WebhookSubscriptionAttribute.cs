namespace Bum.EventGrid.Subscriptions.Annotations
{
    public class WebhookSubscriptionAttribute : SubscriptionAttribute
    {
        /// <summary>
        /// The fully qualified webhook url. For azure functions it should be https://function host/runtime/webhooks/eventgrid?functionname=name of the function
        /// </summary>
        public string Endpoint { get; set; }
    }
}