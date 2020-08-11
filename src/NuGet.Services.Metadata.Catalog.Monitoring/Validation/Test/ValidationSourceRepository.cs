// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <remarks>
    /// This class exists to add some extra logic to the construction of <see cref="SourceRepository"/> using DI.
    /// </remarks>
    public class ValidationSourceRepository : SourceRepository
    {
        public ValidationSourceRepository(
            PackageSource source,
            IEnumerable<Lazy<INuGetResourceProvider>> lazyProviders,
            IEnumerable<INuGetResourceProvider> providers, 
            FeedType type)
            : base(
                  source, 
                  providers
                    .Select(p => new Lazy<INuGetResourceProvider>(() => p))
                    .Concat(lazyProviders),
                  type)
        {
        }
    }
}
