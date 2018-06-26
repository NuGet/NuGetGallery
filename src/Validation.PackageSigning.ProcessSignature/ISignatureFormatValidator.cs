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
        /// Run all validations on the package's author signature. This includes integrity and trust validations.
        /// </summary>
        /// <param name="package">The package whose author signature should be validated.</param>
        /// <param name="token"></param>
        /// <returns>The result of the author signature's verification.</returns>
        Task<VerifySignaturesResult> ValidateAuthorSignatureAsync(
            ISignedPackageReader package,
            CancellationToken token);

        /// <summary>
        /// Run all validations on the package's repository signature. This includes integrity and trust validations.
        /// </summary>
        /// <param name="package">The package whose repository signature should be validated.</param>
        /// <param name="token"></param>
        /// <returns>The result of the repository signature's verification.</returns>
        Task<VerifySignaturesResult> ValidateRepositorySignatureAsync(
            ISignedPackageReader package,
            CancellationToken token);

        /// <summary>
        /// Run all validations on the package's signature. This includes integrity and trust validations.
        /// </summary>
        /// <param name="package">The package to validate.</param>
        /// <param name="hasRepositorySignature">If false, skips the certificate allow list verification of the repository signature.</param>
        /// <param name="token"></param>
        /// <returns>The result of the package's signature(s) verification.</returns>
        Task<VerifySignaturesResult> ValidateAllSignaturesAsync(
            ISignedPackageReader package,
            bool hasRepositorySignature, // TODO: Remove parameter once this is fixed: https://github.com/NuGet/Home/issues/7042
            CancellationToken token);
    }
}