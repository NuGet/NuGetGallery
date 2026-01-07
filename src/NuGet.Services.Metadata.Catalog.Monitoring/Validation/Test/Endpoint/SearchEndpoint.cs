// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            IReadOnlyList<SearchCursorConfiguration> cursors,
            Uri baseUri,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            Cursor = AggregateCursors(cursors, messageHandlerFactory);
            InstanceName = instanceName;
            BaseUri = baseUri;
        }

        public ReadCursor Cursor { get; }
        public string InstanceName { get; }
        public Uri BaseUri { get; }

        private static ReadCursor AggregateCursors(
            IReadOnlyList<SearchCursorConfiguration> cursors,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            var innerCursors = new List<ReadCursor>();
            foreach (var config in cursors)
            {
                if (config.BlobClient is not null)
                {
                    innerCursors.Add(new AzureBlobCursor(config.BlobClient));
                }
                else
                {
                    innerCursors.Add(new HttpReadCursor(config.CursorUri, messageHandlerFactory));
                }
            }

            return new AggregateCursor(innerCursors);
        }
    }
}
