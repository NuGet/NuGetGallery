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

        protected async Task<PackageRegistrationIndexMetadata> GetIndexAsync(
            IPackageRegistrationMetadataResource resource,
            ValidationContext context)
        {
            return await resource.GetIndexAsync(context.Package, Logger.AsCommon(), context.CancellationToken);
        }

        protected async Task<PackageRegistrationLeafMetadata> GetLeafAsync(
            IPackageRegistrationMetadataResource resource,
            ValidationContext context)
        {
            return await resource.GetLeafAsync(context.Package, Logger.AsCommon(), context.CancellationToken);
        }

        protected static class Keys
        {
            internal static readonly string GetIndexAsyncV2 = $"{nameof(RegistrationValidator)}_{nameof(GetIndexAsync)}_V2";
            internal static readonly string GetIndexAsyncV3 = $"{nameof(RegistrationValidator)}_{nameof(GetIndexAsync)}_V3";
            internal static readonly string GetLeafAsyncV2 = $"{nameof(RegistrationValidator)}_{nameof(GetLeafAsync)}_V2";
            internal static readonly string GetLeafAsyncV3 = $"{nameof(RegistrationValidator)}_{nameof(GetLeafAsync)}_V3";
            internal static readonly string ShouldRunAsync = $"{nameof(Validator)}_{nameof(ShouldRunAsync)}";
            internal static readonly string ShouldRunIndexAsync = $"{nameof(RegistrationIndexValidator)}_{nameof(ShouldRunIndexAsync)}";
        }
    }
}