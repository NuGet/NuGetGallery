// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingIdVersionCollector : SortingCollector<FeedPackageIdentity>
    {
        public SortingIdVersionCollector(Uri index, ITelemetryService telemetryService, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, telemetryService, handlerFunc)
        {
        }

        protected override FeedPackageIdentity GetKey(CatalogCommitItem item)
        {
            return new FeedPackageIdentity(item.PackageIdentity);
        }
    }
}