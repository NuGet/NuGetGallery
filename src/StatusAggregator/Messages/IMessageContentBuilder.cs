// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    /// <summary>
    /// Used by <see cref="MessageFactory"/> to build content for <see cref="MessageEntity"/>s.
    /// </summary>
    public interface IMessageContentBuilder
    {
        /// <summary>
        /// Builds contents for a message of type <paramref name="type"/> affecting <paramref name="component"/>.
        /// </summary>
        string Build(MessageType type, IComponent component);

        /// <summary>
        /// Builds contents for a message of type <paramref name="type"/> affecting <paramref name="component"/> with status <paramref name="status"/>.
        /// </summary>
        string Build(MessageType type, IComponent component, ComponentStatus status);
    }
}