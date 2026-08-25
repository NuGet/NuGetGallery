// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageRegistrationMetadataResourceV3 : IPackageRegistrationMetadataResource
    {
        private RegistrationResourceV3 _registration;
        private HttpSource _client;

        public PackageRegistrationMetadataResourceV3(
            RegistrationResourceV3 registration,
            HttpSource client)
        {
            _registration = registration;
            _client = client;
        }

        public async Task<PackageRegistrationIndexMetadata> GetIndexAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageFromIndexAsync(package, log, token);
                if (feedPackage == null)
                {
                    return null;
                }

                var metadata = JsonConvert.DeserializeObject<PackageRegistrationIndexMetadata>(feedPackage.ToString());

                // Sponsorship URLs are a registration-scoped (ID-level) attribute stored on the registration index
                // root, not on an individual version's catalog entry. They must be read from the index root
                // separately because <see cref="GetPackageFromIndexAsync"/> returns only the per-version metadata.
                metadata.SponsorshipUrls = await GetSponsorshipUrlsFromIndexRootAsync(package, log, token);

                return metadata;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationIndexMetadata)} from V3!", e);
            }
        }

        public async Task<PackageRegistrationLeafMetadata> GetLeafAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            try
            {
                var feedPackage = await GetPackageFromLeafAsync(package, log, token);
                return feedPackage != null ?
                    JsonConvert.DeserializeObject<PackageRegistrationLeafMetadata>(feedPackage.ToString()) :
                    null;
            }
            catch (Exception e)
            {
                throw new ValidationException($"Could not fetch {nameof(PackageRegistrationLeafMetadata)} from V3!", e);
            }
        }

        private Task<JObject> GetPackageFromIndexAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            // If the registration index is missing, this will return null.
            return _registration.GetPackageMetadata(package, NullSourceCacheContext.Instance, log, token);
        }

        private async Task<List<string>> GetSponsorshipUrlsFromIndexRootAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            // If the registration index is missing, IgnoreNotFounds will cause this to return null.
            var indexRoot = await _client.GetJObjectAsync(
                new HttpSourceRequest(
                    _registration.GetUri(package.Id), log)
                { IgnoreNotFounds = true },
                log, token);

            // Sponsorship URLs are stored as a candidate within the registration index's root-level "metadata"
            // container (rather than as a root-level property).
            var sponsorshipUrls = indexRoot?["metadata"]?["sponsorshipUrls"] as JArray;
            if (sponsorshipUrls == null)
            {
                return null;
            }

            return sponsorshipUrls.Values<string>().ToList();
        }

        private async Task<JObject> GetPackageFromLeafAsync(PackageIdentity package, ILogger log, CancellationToken token)
        {
            /// If the registration leaf is missing, <see cref="HttpSourceRequest.IgnoreNotFounds"/> will cause this to return null.
            return await _client.GetJObjectAsync(
                new HttpSourceRequest(
                    _registration.GetUri(package), log)
                { IgnoreNotFounds = true },
                log, token);
        }
    }
}
