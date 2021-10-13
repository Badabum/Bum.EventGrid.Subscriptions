using System;
using System.Threading.Tasks;
using Azure.Messaging.EventGrid;
using Bum.EventGrid.Subscriptions.Annotations;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bum.EventGrid.Subscrioptions.AzureFunctionsIsolatedSample
{
    public class Trigger
    {
        private readonly ILogger<Trigger> _logger;

        public Trigger(ILogger<Trigger> logger)
        {
            _logger = logger;
        }

        [FunctionAppSubscription(
            FunctionName = nameof(EventGridTriggeredFunc),
            Name = "TestSubscription",
            EventTypes = new []{"TestEvent"},
            FunctionAppName = "sample-function-app-{environment}",
            ResourceName = "topic-name-{environment}",
            ResourceType = EventResourceType.EventGridTopic)]
        [Function(nameof(EventGridTriggeredFunc))]
        public async Task EventGridTriggeredFunc([EventGridTrigger] EventGridEvent evt)
        {
            _logger.LogInformation("Event triggered function");
        }
    }
}