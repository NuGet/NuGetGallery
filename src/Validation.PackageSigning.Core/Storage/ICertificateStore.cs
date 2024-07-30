// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    /// <summary>
    /// The interface used to <see cref="X509Certificate2"/> store and retrieve certificates.
    /// </summary>
    public interface ICertificateStore
    {
        /// <summary>
        /// Check if the store contains the certificate.
        /// </summary>
        /// <param name="thumbprint">The certificate's thumbprint.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Whether the store contains a certificate that has the given thumbprint.</returns>
        Task<bool> ExistsAsync(string thumbprint, CancellationToken cancellationToken);

        /// <summary>
        /// Load the certificate into memory.
        /// </summary>
        /// <param name="thumbprint">The certificate's thumbprint.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A certificate whose thumbprint is the given thumbprint.</returns>
        Task<X509Certificate2> LoadAsync(string thumbprint, CancellationToken cancellationToken);

        /// <summary>
        /// Save the certificate to the store. This method fails if the certificate already exists.
        /// </summary>
        /// <param name="certificate">The certificate to save to the store.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that completes when the certificate has been saved.</returns>
        Task SaveAsync(X509Certificate2 certificate, CancellationToken cancellationToken);
    }
}
