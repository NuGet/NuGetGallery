// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface ISignatureFormatValidator
    {
        /// <summary>
        /// Verify that the package's signature is readable. Does not perform integrity or trust validations.
        /// </summary>
        /// <param name="package">The package to validate</param>
        /// <param name="token"></param>
        /// <returns>Whether the package's signature is readable.</returns>
        Task<VerifySignaturesResult> ValidateMinimalAsync(
            ISignedPackageReader package,
            CancellationToken token);

        /// <summary>
        /// Run all validations on the package's signature. This includes integrity and trust validations.
        /// </summary>
        /// <param name="package">The package to validate.</param>
        /// <param name="hasRepositorySignature">If false, skips the certificate allow list verification of the repository signature.</param>
        /// <param name="token"></param>
        /// <returns>Whether the package's signature is valid.</returns>
        Task<VerifySignaturesResult> ValidateFullAsync(
            ISignedPackageReader package,
            bool hasRepositorySignature,
            CancellationToken token);
    }
}