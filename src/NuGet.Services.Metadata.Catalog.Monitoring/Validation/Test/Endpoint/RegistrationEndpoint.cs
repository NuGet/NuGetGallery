// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the registration blobs endpoint, which stores metadata about packages.
    /// </summary>
    public class RegistrationEndpoint : Endpoint
    {
        public RegistrationEndpoint(
            EndpointConfiguration config, 
            Func<HttpMessageHandler> messageHandlerFactory)
            : base(config.RegistrationCursorUri, messageHandlerFactory)
        {
        }
    }
}
