// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Validates that the package is repository signed.
    /// </summary>
    public class PackageIsRepositorySignedValidator : FlatContainerValidator
    {
        public PackageIsRepositorySignedValidator(
            FlatContainerEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<PackageIsRepositorySignedValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        protected async override Task<ShouldRunTestResult> ShouldRunAsync(ValidationContext context)
        {
            if (!Config.RequireRepositorySignature)
            {
                return ShouldRunTestResult.No;
            }

            return await ShouldRunTestUtility.Combine(
                () => base.ShouldRunAsync(context), 
                () => PackageExistsAsync(context));
        }

        protected async override Task RunInternalAsync(ValidationContext context)
        {
            // Get the package's signature, if any.
            var signature = await GetPrimarySignatureOrNullAsync(context);

            if (signature == null)
            {
                throw new MissingRepositorySignatureException(
                    $"Package {context.Package.Id} {context.Package.Version} is unsigned.",
                    MissingRepositorySignatureReason.Unsigned);
            }

            // The repository signature can be the primary signature or the author signature's countersignature.
            IRepositorySignature repositorySignature = null;

            switch (signature.Type)
            {
                case SignatureType.Repository:
                    repositorySignature = (RepositoryPrimarySignature)signature;
                    break;

                case SignatureType.Author:
                    repositorySignature = RepositoryCountersignature.GetRepositoryCountersignature(signature);

                    if (repositorySignature == null)
                    {
                        throw new MissingRepositorySignatureException(
                            $"Package {context.Package.Id} {context.Package.Version} is author signed but not repository signed.",
                            MissingRepositorySignatureReason.AuthorSignedNoRepositoryCountersignature);
                    }

                    break;

                default:
                case SignatureType.Unknown:
                    throw new MissingRepositorySignatureException(
                        $"Package {context.Package.Id} {context.Package.Version} has an unknown signature type '{signature.Type}'.",
                        MissingRepositorySignatureReason.UnknownSignature);
            }

            Logger.LogInformation(
                "Package {PackageId} {PackageVersion} has a repository signature with service index {ServiceIndex} and owners {Owners}.",
                context.Package.Id,
                context.Package.Version,
                repositorySignature.V3ServiceIndexUrl,
                repositorySignature.PackageOwners);
        }

        private async Task<PrimarySignature> GetPrimarySignatureOrNullAsync(ValidationContext context)
        {
            var downloader = new PackageDownloader(context.Client, Logger);
            var uri = GetV3PackageUri(context);

            using (var packageStream = await downloader.DownloadAsync(uri, context.CancellationToken))
            {
                if (packageStream == null)
                {
                    throw new InvalidOperationException($"Package {context.Package.Id} {context.Package.Version} couldn't be downloaded at {uri.AbsoluteUri}.");
                }

                using (var package = new PackageArchiveReader(packageStream))
                {
                    return await package.GetPrimarySignatureAsync(context.CancellationToken);
                }
            }
        }

        private async Task<ShouldRunTestResult> PackageExistsAsync(ValidationContext context)
        {
            var uri = GetV3PackageUri(context);

            using (var request = new HttpRequestMessage(HttpMethod.Head, uri))
            using (var response = await context.Client.SendAsync(request))
            {
                return response.IsSuccessStatusCode ? ShouldRunTestResult.Yes : ShouldRunTestResult.No;
            }
        }
    }
}