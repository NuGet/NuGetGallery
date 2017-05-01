// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the flat-container blobs endpoint, which stores nupkgs for packages and a directory of versions.
    /// </summary>
    public class FlatContainerEndpoint : EndpointValidator
    {
        public FlatContainerEndpoint(ReadCursor cursor, ValidatorFactory factory, ILogger<FlatContainerEndpoint> logger)
            : base(cursor, factory, logger)
        {
        }
    }
}
