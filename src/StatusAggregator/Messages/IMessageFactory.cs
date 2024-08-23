// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Handles creating, updating, and delete <see cref="MessageEntity"/>s.
    /// </summary>
    public interface IMessageFactory
    {
        /// <summary>
        /// Creates a message for <paramref name="eventEntity"/> at <paramref name="time"/> of type <paramref name="type"/> affecting <paramref name="component"/>.
        /// </summary>
        Task CreateMessageAsync(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);

        /// <summary>
        /// Creates a message for <paramref name="eventEntity"/> at <paramref name="time"/> of type <paramref name="type"/> affecting <paramref name="component"/> with status <paramref name="status"/>.
        /// </summary>
        Task CreateMessageAsync(EventEntity eventEntity, DateTime time, MessageType type, IComponent component, ComponentStatus status);

        /// <summary>
        /// Updates the message for <paramref name="eventEntity"/> at <paramref name="time"/> of type <paramref name="type"/> affecting <paramref name="component"/>.
        /// </summary>
        Task UpdateMessageAsync(EventEntity eventEntity, DateTime time, MessageType type, IComponent component);
    }
}