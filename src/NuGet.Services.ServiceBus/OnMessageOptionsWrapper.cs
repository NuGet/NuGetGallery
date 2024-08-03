// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Messaging.ServiceBus;

namespace NuGet.Services.ServiceBus
{
    public sealed class OnMessageOptionsWrapper : IOnMessageOptions
    {
        /// <summary>
        /// Specifies whether the Service Bus message completes when message callback returns.
        /// </summary>
        /// <remarks>
        /// Default value is set to the default value of the respective property of <see cref="OnMessageOptions"/>.
        /// </remarks>
        public bool AutoComplete { get; set; } = true;

        /// <summary>
        /// Specifies the maximum number of concurrent message callbacks.
        /// </summary>
        /// <remarks>
        /// Default value is set to the default value of the respective property of <see cref="OnMessageOptions"/>.
        /// </remarks>
        public int MaxConcurrentCalls { get; set; } = 1;

        internal ServiceBusProcessorOptions GetOptions()
        {
            return new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = AutoComplete,
                MaxConcurrentCalls = MaxConcurrentCalls,
            };
        }
    }
}
