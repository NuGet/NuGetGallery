// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class SignatureValidator : ISignatureValidator
    {
        private const string FormatVerificationName = "format verification";
        private const string SignatureVerificationName = "signature integrity and trust verification";

        private readonly IPackageSigningStateService _packageSigningStateService;
        private readonly IPackageSignatureVerifier _minimalPackageSignatureVerifier;
        private readonly IPackageSignatureVerifier _fullPackageSignatureVerifier;
        private readonly ISignaturePartsExtractor _signaturePartsExtractor;
        private readonly IEntityRepository<Certificate> _certificates;
        private readonly ILogger<SignatureValidator> _logger;

        public SignatureValidator(
            IPackageSigningStateService packageSigningStateService,
            IPackageSignatureVerifier minimalPackageSignatureVerifier,
            IPackageSignatureVerifier fullPackageSignatureVerifier,
            ISignaturePartsExtractor signaturePartsExtractor,
            IEntityRepository<Certificate> certificates,
            ILogger<SignatureValidator> logger)
        {
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _minimalPackageSignatureVerifier = minimalPackageSignatureVerifier ?? throw new ArgumentNullException(nameof(minimalPackageSignatureVerifier));
            _fullPackageSignatureVerifier = fullPackageSignatureVerifier ?? throw new ArgumentNullException(nameof(fullPackageSignatureVerifier));
            _signaturePartsExtractor = signaturePartsExtractor ?? throw new ArgumentNullException(nameof(signaturePartsExtractor));
            _certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SignatureValidatorResult> ValidateAsync(
            int packageKey,
            ISignedPackage signedPackage,
            SignatureValidationMessage message,
            CancellationToken cancellationToken)
        {
            // Reject Zip64 package whether or not they are signed.
            if (await signedPackage.IsZip64Async(cancellationToken))
            {
                return await RejectAsync(packageKey, message, ValidationIssue.PackageIsZip64);
            }

            if (!await signedPackage.IsSignedAsync(cancellationToken))
            {
                return await HandleUnsignedPackageAsync(packageKey, message);
            }
            else
            {
                return await HandleSignedPackageAsync(packageKey, signedPackage, message, cancellationToken);
            }
        }
        
        private async Task<SignatureValidatorResult> HandleUnsignedPackageAsync(int packageKey, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary for {ValidationId}.",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId);

            return await AcceptAsync(packageKey, message, PackageSigningStatus.Unsigned);
        }

        private async Task<SignatureValidatorResult> HandleSignedPackageAsync(
            int packageKey,
            ISignedPackageReader signedPackageReader,
            SignatureValidationMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                // First, detect format errors with a minimal verification. This doesn't even check package integrity. The
                // minimal verification is expected to swallow any sort of signature format exception.
                var invalidFormatResult = await GetVerifyResult(
                    FormatVerificationName,
                    _minimalPackageSignatureVerifier,
                    packageKey,
                    signedPackageReader,
                    message,
                    cancellationToken);
                if (invalidFormatResult != null)
                {
                    return invalidFormatResult;
                }

                // We now know we can safely read the signature.
                var packageSignature = await signedPackageReader.GetSignatureAsync(cancellationToken);

                // Block packages with any unknown signing certificates.
                var packageThumbprint = packageSignature
                    .SignerInfo
                    .Certificate
                    .ComputeSHA256Thumbprint();
                var isKnownCertificate = _certificates
                    .GetAll()
                    .Any(c => packageThumbprint == c.Thumbprint);
                if (!isKnownCertificate)
                {
                    _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has an unknown certificate thumbprint: {UnknownThumbprint}",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId,
                        packageThumbprint);

                    return await RejectAsync(
                        packageKey,
                        message,
                        ValidationIssue.PackageIsSigned);
                }

                // Only reject counter signatures that have the author or repository commitment type. Other types of
                // counter signatures are not produced by the client but technically just fine.
                var authorOrRepositoryCounterSignatureCount = packageSignature
                    .SignerInfo
                    .CounterSignerInfos
                    .Cast<SignerInfo>()
                    .Select(x => AttributeUtility.GetSignatureType(x.SignedAttributes))
                    .Count(signatureType => signatureType != SignatureType.Unknown);
                if (authorOrRepositoryCounterSignatureCount > 0)
                {
                    _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has {AuthorOrRepositorySignatureCount} invalid countersignatures.",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId,
                        authorOrRepositoryCounterSignatureCount);

                    return await RejectAsync(
                        packageKey,
                        message,
                        ValidationIssue.AuthorAndRepositoryCounterSignaturesNotSupported);
                }

                if (packageSignature.Type != SignatureType.Author)
                {
                    _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it is not author signed: {SignatureType}",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId,
                        packageSignature.Type);

                    return await RejectAsync(
                        packageKey,
                        message,
                        ValidationIssue.OnlyAuthorSignaturesSupported);
                }

                // Call the "verify" API, which does the main logic of signature validation.
                var failureResult = await GetVerifyResult(
                    SignatureVerificationName,
                    _fullPackageSignatureVerifier,
                    packageKey,
                    signedPackageReader,
                    message,
                    cancellationToken);
                if (failureResult != null)
                {
                    return failureResult;
                }

                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} for validation {ValidationId} is valid with certificate thumbprint: {PackageThumbprint}",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId,
                    packageThumbprint);
            }
            catch (SignatureException ex)
            {
                EventId eventId = 0;
                _logger.LogInformation(
                    eventId,
                    ex,
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} due to an exception during signature validation.",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId);

                return await RejectAsync(
                    packageKey,
                    message,
                    new ClientSigningVerificationFailure(ex.Code.ToString(), ex.Message));
            }

            // Mark this package as signed. This needs to happen before the extraction due to a foreign key constraint.
            var result = await AcceptAsync(packageKey, message, PackageSigningStatus.Valid);

            // Extract all of the signature artifacts and persist them.
            await _signaturePartsExtractor.ExtractAsync(packageKey, signedPackageReader, cancellationToken);

            return result;
        }

        private async Task<SignatureValidatorResult> GetVerifyResult(
            string verificationName,
            IPackageSignatureVerifier verifier,
            int packageKey,
            ISignedPackageReader signedPackageReader,
            SignatureValidationMessage message,
            CancellationToken cancellationToken)
        {
            var verifyResult = await verifier.VerifySignaturesAsync(
                signedPackageReader,
                cancellationToken);

            var errorIssues = verifyResult
                .Results
                .SelectMany(x => x.GetErrorIssues())
                .ToList();
            var warningsForLogs = verifyResult
                .Results
                .SelectMany(x => x.GetWarningIssues())
                .Select(x => $"{x.Code}: {x.Message}")
                .ToList();
            var errorsForLogs = errorIssues
                .Select(x => $"{x.Code}: {x.Message}")
                .ToList();

            if (!verifyResult.Valid)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked during {VerificationName} for validation {ValidationId} . Errors: [{Errors}] Warnings: [{Warnings}]",
                    message.PackageId,
                    message.PackageVersion,
                    verificationName,
                    message.ValidationId,
                    errorsForLogs,
                    warningsForLogs);

                // Treat the "signature format version" error specially. This is because the message provided by the
                // client does not make sense in the server context.
                IValidationIssue[] errorValidationIssues;
                if (errorIssues.Any(x => x.Code == NuGetLogCode.NU3007))
                {
                    errorValidationIssues = new[]
                    {
                        ValidationIssue.OnlySignatureFormatVersion1Supported,
                    };
                }
                else
                {
                    errorValidationIssues = errorIssues
                        .Select(x => new ClientSigningVerificationFailure(x.Code.ToString(), x.Message))
                        .ToArray();
                }

                return await RejectAsync(
                    packageKey,
                    message,
                    errorValidationIssues);
            }
            else
            {
                _logger.LogInformation(
                   "Signed package {PackageId} {PackageVersion} passed {VerificationName} for validation {ValidationId}. Errors: [{Errors}] Warnings: [{Warnings}]",
                   message.PackageId,
                   message.PackageVersion,
                   verificationName,
                   message.ValidationId,
                   errorsForLogs,
                   warningsForLogs);

                return null;
            }
        }

        private async Task<SignatureValidatorResult> RejectAsync(
            int packageKey,
            SignatureValidationMessage message,
            params IValidationIssue[] issues)
        {
            await _packageSigningStateService.SetPackageSigningState(
                packageKey,
                message.PackageId,
                message.PackageVersion,
                status: PackageSigningStatus.Invalid);

            return new SignatureValidatorResult(ValidationStatus.Failed, issues);
        }

        private async Task<SignatureValidatorResult> AcceptAsync(
            int packageKey,
            SignatureValidationMessage message,
            PackageSigningStatus status)
        {
            await _packageSigningStateService.SetPackageSigningState(
                packageKey,
                message.PackageId,
                message.PackageVersion,
                status);

            return new SignatureValidatorResult(ValidationStatus.Succeeded);
        }
    }
}
