// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationLeafValidator : RegistrationValidator
    {
        public RegistrationLeafValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<RegistrationLeafValidator> logger) : base(feedToSource, logger)
        {
        }

        protected override async Task<bool> ShouldRunAsync(ValidationContext context)
        {
            var shouldRunTask = context.GetCachedResultAsync(
                Keys.ShouldRunAsync,
                new Lazy<Task<bool>>(() => base.ShouldRunAsync(context)));
            var v2Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV2,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V2Resource, context)));
            var v3Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV3,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V3Resource, context)));
            var v2Leaf = await context.GetCachedResultAsync(
                Keys.GetLeafAsyncV2,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(() => GetLeafAsync(V2Resource, context)));
            var v3Leaf = await context.GetCachedResultAsync(
                Keys.GetLeafAsyncV3,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(() => GetLeafAsync(V3Resource, context)));

            return await shouldRunTask
                && await ShouldRunLeafAsync(context, v2Index, v3Index)
                && await ShouldRunLeafAsync(context, v2Leaf, v3Leaf);
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var exceptions = new List<Exception>();

            var v2Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV2,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V2Resource, context)));
            var v3Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV3,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V3Resource, context)));

            try
            {
                await CompareLeafAsync(context, v2Index, v3Index);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e));
            }

            var v2Leaf = await context.GetCachedResultAsync(
                Keys.GetLeafAsyncV2,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(() => GetLeafAsync(V2Resource, context)));
            var v3Leaf = await context.GetCachedResultAsync(
                Keys.GetLeafAsyncV3,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(() => GetLeafAsync(V3Resource, context)));

            try
            {
                await CompareLeafAsync(context, v2Leaf, v3Leaf);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration leaf metadata does not match the Packages(Id='...',Version='...') metadata!", e));
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        public abstract Task<bool> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3);

        public abstract Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3);
    }
}