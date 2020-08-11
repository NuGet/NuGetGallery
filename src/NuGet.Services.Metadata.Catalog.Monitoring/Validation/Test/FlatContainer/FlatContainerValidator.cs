// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The base class for all Package Content (aka "flat container") validations.
    /// </summary>
    public abstract class FlatContainerValidator : Validator<FlatContainerEndpoint>
    {
        public FlatContainerValidator(
            FlatContainerEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<FlatContainerValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        protected Uri GetV3PackageUri(ValidationContext context)
        {
            // Based off DownloadResourceV3
            // See: https://github.com/NuGet/NuGet.Client/blob/3803820961f4d61c06d07b179dab1d0439ec0d91/src/NuGet.Core/NuGet.Protocol/Resources/DownloadResourceV3.cs#L78
            var id = context.Package.Id.ToLowerInvariant();
            var version = context.Package.Version.ToNormalizedString().ToLowerInvariant();

            return new Uri($"{Config.PackageBaseAddress}/{id}/{version}/{id}.{version}.nupkg");
        }
    }
}