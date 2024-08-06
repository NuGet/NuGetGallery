// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// Processes messages that were received from a Service Bus subscription.
    /// </summary>
    /// <typeparam name="TMessage">The type of message listened by this listener.</typeparam>
    public interface ISubscriptionProcessor<TMessage>
    {
        /// <summary>
        /// The number of messages that are currently being handled.
        /// </summary>
        int NumberOfMessagesInProgress { get; }

        /// <summary>
        /// Start handling messages emitted to the Service Bus subscription.
        /// </summary>
        /// <remarks>
        /// MaxConcurrentMessages is defaulted to the number set by the ServiceBus library (seems to be 1)
        /// </remarks>
        Task StartAsync();

        /// <summary>
        /// Start handling messages emitted to the Service Bus subscription.
        /// </summary>
        /// <param name="maxConcurrentCalls">Maximum number of messages processed in parallel.</param>
        Task StartAsync(int maxConcurrentCalls);

        /// <summary>
        /// Deregisters the message handler and waits until currently in-flight messages have been handled.
        /// </summary>
        /// <remarks>
        /// There may still be messages in progress after the returned <see cref="Task"/> has completed!
        /// The <see cref="NumberOfMessagesInProgress"/> property can be polled to determine when all
        /// messages have been completed.
        /// </remarks>
        /// <param name="timeout">The maximum amount of time the shutdown may take.</param>
        /// <returns>A task that completes as true if the shutdown succeeded gracefully.</returns>
        Task<bool> ShutdownAsync(TimeSpan timeout);
    }
}
