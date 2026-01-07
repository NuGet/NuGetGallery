// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public sealed class SearchEndpointConfiguration
    {
        public SearchEndpointConfiguration(IReadOnlyList<SearchCursorConfiguration> cursors, Uri baseUri)
        {
            Cursors = cursors;
            BaseUri = baseUri;
        }

        public IReadOnlyList<SearchCursorConfiguration> Cursors { get; }
        public Uri BaseUri { get; }
    }
}
