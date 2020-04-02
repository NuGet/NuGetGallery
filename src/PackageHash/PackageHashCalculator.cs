// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation;
using NuGet.Packaging.Core;
using NuGetGallery;

namespace NuGet.Services.PackageHash
{
    public class PackageHashCalculator : IPackageHashCalculator
    {
        private readonly IFileDownloader _packageDownloader;

        public PackageHashCalculator(IFileDownloader packageDownloader)
        {
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
        }

        public async Task<string> GetPackageHashAsync(
            PackageSource source,
            PackageIdentity package,
            string hashAlgorithmId,
            CancellationToken token)
        {
            if (source.Type != PackageSourceType.PackagesContainer)
            {
                throw new NotSupportedException($"Only the package source type {PackageSourceType.PackagesContainer} is supported.");
            }

            var id = package.Id.ToLowerInvariant();
            var version = package.Version.ToNormalizedString().ToLowerInvariant();
            var packageUri = new Uri($"{source.Url.TrimEnd('/')}/{id}.{version}.nupkg");

            using (var result = await _packageDownloader.DownloadAsync(packageUri, token))
            {
                return CryptographyService.GenerateHash(result.GetStreamOrThrow(), hashAlgorithmId);
            }
        }
    }
}
