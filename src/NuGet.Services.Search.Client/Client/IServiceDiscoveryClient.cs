// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    /// <summary>
    /// This interface is used to discover the service client.
    /// </summary>
    public interface IServiceDiscoveryClient
    {
        /// <summary>
        /// The function is used to retrieve the endpoint for the resource type.
        /// </summary>
        /// <param name="resourceType"> The resource type needed for retrieval</param>
        Task<IEnumerable<Uri>> GetEndpointsForResourceType(string resourceType);
    }
}