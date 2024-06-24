// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface ICertificateService
    {
        /// <summary>
        /// Add a certificate to the database if the certificate does not already exist.
        /// </summary>
        /// <param name="file">The certificate file.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="Certificate" /> 
        /// entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="file" /> is <c>null</c>.</exception>
        Task<Certificate> AddCertificateAsync(HttpPostedFileBase file);

        /// <summary>
        /// Activates an existing certificate for an account.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <param name="account">The account.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty or if a certificate with the specified thumbprint does not exist.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="account" /> is <c>null</c>.</exception>
        Task ActivateCertificateAsync(string thumbprint, User account);

        /// <summary>
        /// Deactivates an existing certificate for an account.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <param name="account">The account.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty or if a certificate with the specified thumbprint does not exist.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="account" /> is <c>null</c>.</exception>
        Task DeactivateCertificateAsync(string thumbprint, User account);

        /// <summary>
        /// Gets certificates associated with the specified account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <returns>An enumerable of <see cref="Certificate" /> entities.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="account" /> is <c>null</c>.</exception>
        IEnumerable<Certificate> GetCertificates(User account);

        /// <summary>
        /// Adds a certificate pattern for an account. If an equivalent pattern already exists for the user, this method will no-op.
        /// </summary>
        /// <param name="patternType">The certificate pattern type to add.</param>
        /// <param name="identifier">The identifier of the certificate pattern.</param>
        /// <param name="account">The account.</param>
        /// <returns>The user certificate pattern, either existing or newly created.</returns>
        Task<UserCertificatePattern> AddCertificatePatternAsync(CertificatePatternType patternType, string identifier, User account);

        /// <summary>
        /// Deletes a certificate pattern for an account. If a pattern with the provided key does not exist for the given user, this method will no-op.
        /// </summary>
        /// <param name="patternKey">The certificate pattern key.</param>
        /// <param name="account">The account.</param>
        Task DeleteCertificatePatternAsync(int patternKey, User account);

        /// <summary>
        /// Gets certificates patterns associated with the specified account.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <returns>An enumerable of <see cref="UserCertificatePattern" /> entities.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="account" /> is <c>null</c>.</exception>
        IEnumerable<UserCertificatePattern> GetCertificatePatterns(User account);
    }
}