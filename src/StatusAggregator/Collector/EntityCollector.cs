// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    public class EntityCollector : IEntityCollector
    {
        private readonly ICursor _cursor;
        private readonly IEntityCollectorProcessor _processor;

        public EntityCollector(
            ICursor cursor,
            IEntityCollectorProcessor processor)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        public string Name => _processor.Name;

        public async Task<DateTime> FetchLatest()
        {
            var lastCursor = await _cursor.Get(Name);
            var nextCursor = await _processor.FetchSince(lastCursor);
            if (nextCursor.HasValue)
            {
                await _cursor.Set(Name, nextCursor.Value);
            }

            return nextCursor ?? lastCursor;
        }
    }
}