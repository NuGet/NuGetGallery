// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the search endpoint, which allows querying for packages using keywords, by prefix (autocomplete),
    /// and hijack.
    /// </summary>
    public class SearchEndpoint : IEndpoint
    {
        public SearchEndpoint(
            string instanceName,
            IReadOnlyList<Uri> cursorUris,
            Uri baseUri,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            Cursor = new AggregateCursor(cursorUris.Select(c => new HttpReadCursor(c, messageHandlerFactory)));
            InstanceName = instanceName;
            BaseUri = baseUri;
        }

        public ReadCursor Cursor { get; }
        public string InstanceName { get; }
        public Uri BaseUri { get; }
    }
}
