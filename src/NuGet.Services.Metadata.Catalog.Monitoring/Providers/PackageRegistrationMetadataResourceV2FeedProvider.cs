// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceV2FeedProvider : ResourceProvider
    {
        public PackageRegistrationMetadataResourceV2FeedProvider() :
            base(typeof(IPackageRegistrationMetadataResource),
                nameof(IPackageRegistrationMetadataResource))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            IPackageRegistrationMetadataResource resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {
                var serviceDocumentResource = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                var feedParser = new V2FeedParser(httpSourceResource.HttpSource, serviceDocumentResource.BaseAddress, source.PackageSource.Source);

                resource = new PackageRegistrationMetadataResourceV2Feed(feedParser);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
