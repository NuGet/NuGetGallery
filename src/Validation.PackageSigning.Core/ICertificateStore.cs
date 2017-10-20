// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.PackageSigning
{
    /// <summary>
    /// The class used to <see cref="X509Certificate2"/> store and retrieve certificates.
    /// </summary>
    public interface ICertificateStore
    {
        /// <summary>
        /// Check if the store contains the certificate.
        /// </summary>
        /// <param name="thumbprint">The certificate's thumbprint.</param>
        /// <returns>Whether the store contains a certificate that has the given thumbprint.</returns>
        Task<bool> Exists(string thumbprint);

        /// <summary>
        /// Load the certificate into memory.
        /// </summary>
        /// <param name="thumbprint">The certificate's thumbprint.</param>
        /// <returns>A certificate whose thumbprint is the given thumbprint.</returns>
        Task<X509Certificate2> Load(string thumbprint);

        /// <summary>
        /// Save the certificate to the store.
        /// </summary>
        /// <param name="certificate">The certificate to save to the store.</param>
        /// <returns>A task that completes when the certificate has been saved.</returns>
        Task Save(X509Certificate2 certificate);
    }
}
