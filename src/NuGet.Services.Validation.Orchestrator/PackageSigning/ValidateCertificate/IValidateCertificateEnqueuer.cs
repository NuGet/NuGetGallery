// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// Kicks off certificate verification.
    /// </summary>
    public interface IValidateCertificateEnqueuer
    {
        /// <summary>
        /// Kicks off the certificate verification process for the given request. Verification will begin when the
        /// <see cref="ValidationEntitiesContext"/> has a <see cref="EndCertificateValidation"/> that matches the
        /// <see cref="INuGetValidationRequest"/>'s validationId. Once verification completes, the <see cref="CertificateValidation"/>'s
        /// State will be updated to a non-NULL value.
        /// </summary>
        /// <param name="request">The request that details the package to be verified.</param>
        /// <param name="certificate">The certificate to verify.</param>
        /// <returns>A task that will complete when the verification process has been queued.</returns>
        Task EnqueueVerificationAsync(INuGetValidationRequest request, EndCertificate certificate);
    }
}