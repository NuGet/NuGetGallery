// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// Serializes objects into Service Bus <see cref="IBrokeredMessage"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type this serializer can serialize into <see cref="IBrokeredMessage"/>.</typeparam>
    public interface IBrokeredMessageSerializer<TMessage>
    {
        /// <summary>
        /// Serialize a <see cref="TMessage"/> into a <see cref="IBrokeredMessage"/>.
        /// </summary>
        /// <param name="message">The message to be serialized.</param>
        /// <returns>The serialized message.</returns>
        IBrokeredMessage Serialize(TMessage message);

        /// <summary>
        /// Deserialize a <see cref="IBrokeredMessage"/> into a <see cref="TMessage"/>.
        /// </summary>
        /// <param name="message">The message to be deserialized.</param>
        /// <returns>The deserialized message.</returns>
        TMessage Deserialize(IReceivedBrokeredMessage message);
    }
}
