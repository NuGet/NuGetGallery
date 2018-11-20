// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using NuGet.Protocol.Catalog;

namespace NuGet.Protocol.Registration
{
    public class RegistrationClient : IRegistrationClient
    {
        private readonly ISimpleHttpClient _simpleHttpClient;

        public RegistrationClient(ISimpleHttpClient simpleHttpClient)
        {
            _simpleHttpClient = simpleHttpClient ?? throw new ArgumentNullException(nameof(simpleHttpClient));
        }

        public async Task<RegistrationIndex> GetIndexOrNullAsync(string indexUrl)
        {
            var result = await _simpleHttpClient.DeserializeUrlAsync<RegistrationIndex>(indexUrl);
            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return result.GetResultOrThrow();
        }

        public async Task<RegistrationPage> GetPageAsync(string pageUrl)
        {
            var result = await _simpleHttpClient.DeserializeUrlAsync<RegistrationPage>(pageUrl);
            return result.GetResultOrThrow();
        }

        public async Task<RegistrationLeaf> GetLeafAsync(string leafUrl)
        {
            var result = await _simpleHttpClient.DeserializeUrlAsync<RegistrationLeaf>(leafUrl);
            return result.GetResultOrThrow();
        }
    }
}
