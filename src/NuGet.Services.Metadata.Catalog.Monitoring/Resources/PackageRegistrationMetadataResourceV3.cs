// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
                return feedPackage != null ?
                    JsonConvert.DeserializeObject<PackageRegistrationIndexMetadata>(feedPackage.ToString()) :
                    null;
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
