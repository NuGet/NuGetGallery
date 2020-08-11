// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A <see cref="ReadCursor"/> that returns the least value for <see cref="LoadAsync(CancellationToken)"/> from a set of <see cref="ReadCursor"/>s.
    /// </summary>
    public class AggregateCursor : ReadCursor
    {
        public AggregateCursor(IEnumerable<ReadCursor> innerCursors)
        {
            if (innerCursors == null || !innerCursors.Any())
            {
                throw new ArgumentException("Must supply at least one cursor!", nameof(innerCursors));
            }

            InnerCursors = innerCursors.ToList();
        }

        public IEnumerable<ReadCursor> InnerCursors { get; private set; }

        public override async Task LoadAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(InnerCursors.Select(c => c.LoadAsync(cancellationToken)));
            Value = InnerCursors.Min(c => c.Value);
        }
    }
}