// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IMessageChangeEventProvider
    {
        /// <summary>
        /// Returns the <see cref="MessageChangeEvent"/>s associated with <see cref="EventEntity"/> given <paramref name="cursor"/>.
        /// </summary>
        IEnumerable<MessageChangeEvent> Get(EventEntity eventEntity, DateTime cursor);
    }
}