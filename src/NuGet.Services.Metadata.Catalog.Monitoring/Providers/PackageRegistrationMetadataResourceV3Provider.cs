// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceV3Provider : ResourceProvider
    {
        public PackageRegistrationMetadataResourceV3Provider() :
            base(typeof(IPackageRegistrationMetadataResource),
                nameof(IPackageRegistrationMetadataResource))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            IPackageRegistrationMetadataResource resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV3)
            {
                var registration = await source.GetResourceAsync<RegistrationResourceV3>(token);
                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);

                resource = new PackageRegistrationMetadataResourceV3(registration, httpSourceResource.HttpSource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
