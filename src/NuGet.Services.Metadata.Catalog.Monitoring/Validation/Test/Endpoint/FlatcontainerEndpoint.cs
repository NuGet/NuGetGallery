// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the flat-container blobs endpoint, which stores nupkgs for packages and a directory of versions.
    /// </summary>
    public class FlatContainerEndpoint : Endpoint
    {
        public FlatContainerEndpoint(
            EndpointConfiguration config,
            Func<HttpMessageHandler> messageHandlerFactory)
            : base(config.FlatContainerCursorUri, messageHandlerFactory)
        {
        }
    }
}
