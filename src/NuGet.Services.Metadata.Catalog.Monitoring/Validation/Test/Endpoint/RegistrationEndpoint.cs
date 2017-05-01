// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the registration blobs endpoint, which stores metadata about packages.
    /// </summary>
    public class RegistrationEndpoint : EndpointValidator
    {
        public RegistrationEndpoint(ReadCursor cursor, ValidatorFactory factory, ILogger<RegistrationEndpoint> logger)
            : base(cursor, factory, logger)
        {
        }
    }
}
