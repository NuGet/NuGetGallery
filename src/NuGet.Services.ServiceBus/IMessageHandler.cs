// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// The class that handles messages received by a <see cref="ISubscriptionProcessor{TMessage}"/>
    /// </summary>
    /// <typeparam name="TMessage">The type of messages this handler handles.</typeparam>
    public interface IMessageHandler<TMessage>
    {
        /// <summary>
        /// Handle the message.
        /// </summary>
        /// <param name="message">The received message.</param>
        /// <returns>Whether the message has been handled. If false, the message will be requeued to be handled again later.</returns>
        Task<bool> HandleAsync(TMessage message);
    }
}
