// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationIndexValidator : RegistrationValidator
    {
        public RegistrationIndexValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<RegistrationIndexValidator> logger) : base(feedToSource, logger)
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
            var shouldRunIndexTask = context.GetCachedResultAsync(
                Keys.ShouldRunIndexAsync,
                new Lazy<Task<bool>>(() => ShouldRunIndexAsync(context, v2Index, v3Index)));

            return await shouldRunTask && await shouldRunIndexTask;
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var v2Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV2,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V2Resource, context)));
            var v3Index = await context.GetCachedResultAsync(
                Keys.GetIndexAsyncV3,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(() => GetIndexAsync(V3Resource, context)));

            try
            {
                await CompareIndexAsync(context, v2Index, v3Index);
            }
            catch (Exception e)
            {
                throw new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e);
            }
        }

        public Task<bool> ShouldRunIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata v2,
            PackageRegistrationIndexMetadata v3)
        {
            return Task.FromResult(v2 != null && v3 != null);
        }

        public abstract Task CompareIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata v2,
            PackageRegistrationIndexMetadata v3);
    }
}