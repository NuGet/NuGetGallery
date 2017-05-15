// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;

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

        public async Task<PackageRegistrationIndexMetadata> GetIndex(PackageIdentity package, ILogger log, CancellationToken token)
        {
            var feedPackage = await GetPackageFromIndex(package, log, token);
            return feedPackage != null ? 
                JsonConvert.DeserializeObject<PackageRegistrationIndexMetadata>(feedPackage.ToString()) : 
                null;
        }

        public async Task<PackageRegistrationLeafMetadata> GetLeaf(PackageIdentity package, ILogger log, CancellationToken token)
        {
            var feedPackage = await GetPackageFromLeaf(package, log, token);
            return feedPackage != null ?
                JsonConvert.DeserializeObject<PackageRegistrationLeafMetadata>(feedPackage.ToString()) :
                null;
        }

        private async Task<JObject> GetPackageFromIndex(PackageIdentity package, ILogger log, CancellationToken token)
        {
            return await _registration.GetPackageMetadata(package, log, token);
        }

        private Task<JObject> GetPackageFromLeaf(PackageIdentity package, ILogger log, CancellationToken token)
        {
            return _client.GetJObjectAsync(
                new HttpSourceRequest(
                    _registration.GetUri(package), log), 
                log, token);
        }
    }
}
