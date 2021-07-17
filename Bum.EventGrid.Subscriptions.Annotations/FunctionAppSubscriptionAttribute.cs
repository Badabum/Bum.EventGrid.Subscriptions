﻿using System;

namespace superfeed.eventgrid.annotations
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class FunctionAppSubscriptionAttribute : SubscriptionAttribute
    {
        /// <summary>
        /// Name of the function
        /// </summary>
        public string FunctionName { get; set; }
        /// <summary>
        /// Name of the function app. Supported placeholders {environment}
        /// </summary>
        public string FunctionAppName { get; set; }
    }
}