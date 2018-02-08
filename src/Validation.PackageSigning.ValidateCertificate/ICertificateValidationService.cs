// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    public interface ICertificateValidationService
    {
        /// <summary>
        /// Find the <see cref="CertificateValidation"/> for the given <see cref="CertificateValidationMessage"/>.
        /// </summary>
        /// <param name="message">The message requesting a certificate validation.</param>
        /// <returns>The entity representing the certificate validation's state, or null if one could not be found.</returns>
        Task<EndCertificateValidation> FindCertificateValidationAsync(CertificateValidationMessage message);

        /// <summary>
        /// Update the requested <see cref="CertificateValidation"/> with the <see cref="CertificateVerificationResult"/>.
        /// This may kick off alerts if packages are invalidated!
        /// </summary>
        /// <param name="validation">The validation that should be updated.</param>
        /// <param name="result">Whether the save operation was successful.</param>
        Task<bool> TrySaveResultAsync(EndCertificateValidation validation, CertificateVerificationResult result);
    }
}
