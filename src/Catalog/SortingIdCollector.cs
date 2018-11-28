// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingIdCollector : SortingCollector<string>
    {
        public SortingIdCollector(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            IHttpRetryStrategy retryStrategy = null)
            : base(index, telemetryService, handlerFunc, retryStrategy)
        {
        }

        protected override string GetKey(CatalogCommitItem item)
        {
            return item.PackageIdentity.Id;
        }
    }
}