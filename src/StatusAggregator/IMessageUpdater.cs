// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    /// <summary>
    /// Handles updating <see cref="MessageEntity"/>s for an <see cref="EventEntity"/>.
    /// </summary>
    public interface IMessageUpdater
    {
        /// <summary>
        /// Posts a <see cref="MessageEntity"/> for the start of <paramref name="eventEntity"/>.
        /// </summary>
        /// <param name="cursor">Used to determine whether or not the message should be posted.</param>
        Task CreateMessageForEventStart(EventEntity eventEntity, DateTime cursor);

        /// <summary>
        /// Posts a <see cref="MessageEntity"/> for the end of <paramref name="eventEntity"/>.
        /// </summary>
        Task CreateMessageForEventEnd(EventEntity eventEntity);
    }
}
