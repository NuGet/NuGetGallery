// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class EmptySecretReader : ISecretReader
    {
        public Task<string> GetSecretAsync(string secretName)
        {
            return Task.FromResult(secretName);
        }

        public Task<X509Certificate2> GetCertificateSecretAsync(string secretName, string password)
        {
            return Task.FromResult<X509Certificate2>(null);
        }
    }
}