// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IMessageChangeEventIterator
    {
        /// <summary>
        /// Iterates through <paramref name="changes"/> and processes them.
        /// </summary>
        Task IterateAsync(IEnumerable<MessageChangeEvent> changes, EventEntity eventEntity);
    }
}