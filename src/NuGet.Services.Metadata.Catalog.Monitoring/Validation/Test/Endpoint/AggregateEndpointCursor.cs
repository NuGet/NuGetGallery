// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// An <see cref="AggregateCursor"/> based on a list of <see cref="IEndpoint"/>s.
    /// </summary>
    public class AggregateEndpointCursor : AggregateCursor
    {
        public AggregateEndpointCursor(IEnumerable<IEndpoint> endpoints)
            : base(endpoints.Select(e => e.Cursor))
        {
        }
    }
}
