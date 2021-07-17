using System;

namespace superfeed.eventgrid.annotations
{
    public class SubscriptionAttribute : Attribute
    {
        /// <summary>
        /// Name of the subscription
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The subject that will be used for event mathing. Maps to SubjectBeginsWith filter
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// Type of the resource that is a source of events.
        /// </summary>
        public EventResourceType ResourceType { get; set; }
        /// <summary>
        /// Name of the resource
        /// </summary>
        public string ResourceName { get; set; }
        /// <summary>
        /// Event types to match against. Superfeed produced events have "prod" value.
        /// <example>Microsoft.Storage.BlobCreated</example>
        /// <example>Microsoft.Storage.BlobRemoved</example>
        /// <example>prod</example>
        /// </summary>
        public string[] EventTypes { get; set; }
    }
}