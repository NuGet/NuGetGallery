// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The base class for all Package Content (aka "flat container") validations.
    /// </summary>
    public abstract class FlatContainerValidator : Validator<FlatContainerEndpoint>
    {
        private Lazy<string> _v3PackageBaseAddress;

        public FlatContainerValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<FlatContainerValidator> logger) : base(feedToSource, logger)
        {
            _v3PackageBaseAddress = new Lazy<string>(() =>
            {
                return GetV3PackageBaseAddressAsync(feedToSource);
            });
        }

        private string GetV3PackageBaseAddressAsync(IDictionary<FeedType, SourceRepository> feedToSource)
        {
            // Based off DownloadResourceV3Provider.
            // See: https://github.com/NuGet/NuGet.Client/blob/3803820961f4d61c06d07b179dab1d0439ec0d91/src/NuGet.Core/NuGet.Protocol/Providers/DownloadResourceV3Provider.cs#L19
            var packageBaseAddressResource = feedToSource[FeedType.HttpV3].GetResource<PackageBaseAddressResource>();

            if (packageBaseAddressResource == null)
            {
                throw new ArgumentException(
                    $"Could not find resource {nameof(PackageBaseAddressResource)} on {FeedType.HttpV3} source repository",
                    nameof(feedToSource));
            }

            return packageBaseAddressResource.PackageBaseAddress;
        }

        protected Uri GetV3PackageUri(ValidationContext context)
        {
            // Based off DownloadResourceV3
            // See: https://github.com/NuGet/NuGet.Client/blob/3803820961f4d61c06d07b179dab1d0439ec0d91/src/NuGet.Core/NuGet.Protocol/Resources/DownloadResourceV3.cs#L78
            var id = context.Package.Id.ToLowerInvariant();
            var version = context.Package.Version.ToNormalizedString().ToLowerInvariant();

            return new Uri($"{_v3PackageBaseAddress.Value}/{id}/{version}/{id}.{version}.nupkg");
        }
    }
}