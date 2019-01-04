// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class Endpoint : IEndpoint
    {
        public Endpoint(
            Uri cursorUri,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            Cursor = new HttpReadCursor(cursorUri, messageHandlerFactory);
        }
        
        public ReadCursor Cursor { get; }
    }
}
