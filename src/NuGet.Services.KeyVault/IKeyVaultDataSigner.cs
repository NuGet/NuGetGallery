// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public interface IKeyVaultDataSigner
    {
        /// <summary>
        /// Signs the given data with the specified algorithm using the key stored in key vault.
        /// </summary>
        /// <param name="data">Raw data to sign. Implementation will hash the data internally if required by the algorithm.</param>
        /// <param name="algorithm">Signature algorithm.</param>
        /// <returns>Signature bytes.</returns>
        Task<byte[]> SignDataAsync(byte[] data, KeyVaultSignatureAlgorithm algorithm);
    }
}
