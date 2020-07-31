// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public sealed class SearchEndpointConfiguration
    {
        public SearchEndpointConfiguration(IReadOnlyList<Uri> cursorUris, Uri baseUri)
        {
            CursorUris = cursorUris;
            BaseUri = baseUri;
        }

        public IReadOnlyList<Uri> CursorUris { get; }
        public Uri BaseUri { get; }
    }
}
