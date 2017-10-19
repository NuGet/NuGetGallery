// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        void Start();

        /// <summary>
        /// Deregisters the message handler.
        /// </summary>
        /// <remarks>
        /// There may still be messages in progress after the returned <see cref="Task"/> has completed!
        /// The <see cref="NumberOfMessagesInProgress"/> property should be polled to determine when all
        /// messages have been completed.
        /// </remarks>
        /// <returns>A task that completes when the message handler has been deregistered.</returns>
        Task StartShutdownAsync();
    }
}
