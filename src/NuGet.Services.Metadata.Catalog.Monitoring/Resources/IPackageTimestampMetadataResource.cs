// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public interface IPackageTimestampMetadataResource : INuGetResource
    {
        Task<PackageTimestampMetadata> GetAsync(ValidationContext context);
    }
}