// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.KeyVault
{
    public interface ICachingSecretReader : ISecretReader
    {
        /// <summary>
        /// Remove a secret from the cache.
        /// </summary>
        bool RefreshSecret(string secretName);

        /// <summary>
        /// Remove a certificate secret from the cache.
        /// </summary>
        bool RefreshCertificateSecret(string secretName);
    }
}
