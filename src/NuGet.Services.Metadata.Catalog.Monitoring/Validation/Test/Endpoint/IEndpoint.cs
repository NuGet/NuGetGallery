// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents an endpoint, or V3 resource, to run validations against.
    /// </summary>
    public interface IEndpoint
    {
        /// <summary>
        /// Used to derive the most recent catalog entry for which validations against this endpoint should be ran.
        /// </summary>
        ReadCursor Cursor { get; }
    }
}
