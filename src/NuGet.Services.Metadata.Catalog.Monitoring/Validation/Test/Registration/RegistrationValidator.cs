// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationValidator : Validator<RegistrationEndpoint>
    {
        protected IPackageRegistrationMetadataResource V2Resource;
        protected IPackageRegistrationMetadataResource V3Resource;

        public RegistrationValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<RegistrationValidator> logger) : base(feedToSource, logger)
        {
            V2Resource = feedToSource[FeedType.HttpV2].GetResource<IPackageRegistrationMetadataResource>();
            V3Resource = feedToSource[FeedType.HttpV3].GetResource<IPackageRegistrationMetadataResource>();
        }

        protected async Task<PackageRegistrationIndexMetadata> GetIndex(
            IPackageRegistrationMetadataResource resource,
            ValidationContext data)
        {
            return await resource.GetIndex(data.Package, Logger.AsCommon(), data.CancellationToken);
        }

        protected async Task<PackageRegistrationLeafMetadata> GetLeaf(
            IPackageRegistrationMetadataResource resource,
            ValidationContext data)
        {
            return await resource.GetLeaf(data.Package, Logger.AsCommon(), data.CancellationToken);
        }
    }
}
