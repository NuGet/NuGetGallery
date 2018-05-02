// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using NuGetGallery.Extensions;

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
        private readonly IProcessorPackageFileService _packageFileService;
        private readonly ICorePackageService _corePackageService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SignatureValidator> _logger;

        public SignatureValidator(
            IPackageSigningStateService packageSigningStateService,
            IPackageSignatureVerifier minimalPackageSignatureVerifier,
            IPackageSignatureVerifier fullPackageSignatureVerifier,
            ISignaturePartsExtractor signaturePartsExtractor,
            IProcessorPackageFileService packageFileService,
            ICorePackageService corePackageService,
            ITelemetryService telemetryService,
            ILogger<SignatureValidator> logger)
        {
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _minimalPackageSignatureVerifier = minimalPackageSignatureVerifier ?? throw new ArgumentNullException(nameof(minimalPackageSignatureVerifier));
            _fullPackageSignatureVerifier = fullPackageSignatureVerifier ?? throw new ArgumentNullException(nameof(fullPackageSignatureVerifier));
            _signaturePartsExtractor = signaturePartsExtractor ?? throw new ArgumentNullException(nameof(signaturePartsExtractor));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _corePackageService = corePackageService ?? throw new ArgumentNullException(nameof(corePackageService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SignatureValidatorResult> ValidateAsync(
            int packageKey,
            Stream packageStream,
            SignatureValidationMessage message,
            CancellationToken cancellationToken)
        {
            using (var packageReader = new SignedPackageArchive(packageStream, packageWriteStream: Stream.Null))
            using (var context = new Context(packageKey, packageStream, packageReader, message, cancellationToken))
            {
                // Reject Zip64 package whether or not they are signed.
                if (await context.PackageReader.IsZip64Async(context.CancellationToken))
                {
                    return await RejectAsync(context, ValidationIssue.PackageIsZip64);
                }

                if (await context.PackageReader.IsSignedAsync(cancellationToken))
                {
                    return await HandleSignedPackageAsync(context);
                }

                return await HandleUnsignedPackageAsync(context);
            }
        }

        private async Task<SignatureValidatorResult> HandleUnsignedPackageAsync(Context context)
        {
            var packageRegistration = _corePackageService.FindPackageRegistrationById(context.Message.PackageId);

            if (packageRegistration.IsSigningRequired())
            {
                _logger.LogWarning(
                    "Package {PackageId} {PackageVersion} for validation {ValidationId} must be signed but is unsigned.",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);

                return await RejectAsync(context, ValidationIssue.PackageIsNotSigned);
            }

            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary for {ValidationId}.",
                context.Message.PackageId,
                context.Message.PackageVersion,
                context.Message.ValidationId);

            return await AcceptAsync(context, PackageSigningStatus.Unsigned);
        }

        private async Task<SignatureValidatorResult> HandleSignedPackageAsync(Context context)
        {
            // Validate the package and strip any existing repository signatures.
            var validationResult = await ValidateAndStripRepositorySignatures(context);
            if (validationResult != null)
            {
                return validationResult;
            }

            // Mark this package as signed. This needs to happen before the extraction due to a foreign key constraint.
            var acceptResult = await AcceptAsync(context, PackageSigningStatus.Valid);

            // Extract all of the signature artifacts and persist them.
            await _signaturePartsExtractor.ExtractAsync(
                context.PackageKey,
                context.Signature,
                context.CancellationToken);

            return acceptResult;
        }

        private async Task<SignatureValidatorResult> ValidateAndStripRepositorySignatures(Context context)
        {
            try
            {
                // Perform validations that do not care about repository signatures.
                var initialValidationResult = await PerformInitialValidationsAsync(context);
                if (initialValidationResult != null)
                {
                    return initialValidationResult;
                }

                // Strip all repository signatures.
                var stripRepositorySignaturesResult = await StripRepositorySignaturesAsync(context);
                if (stripRepositorySignaturesResult != null)
                {
                    return stripRepositorySignaturesResult;
                }

                // Perform validations that assume the repository signature is either not present or valid.
                var finalValidationResult = await PerformFinalValidationAsync(context);
                if (finalValidationResult != null)
                {
                    return finalValidationResult;
                }
            }
            catch (SignatureException ex)
            {
                /// Exceptions of <see cref="SignatureException"/> type are error cases in client code that are
                /// explicitly related to signing. Instead of bubbling this error out and retrying, we trust the client
                /// API's categorization of this error case and treat them as a user facing error (which is essentially
                /// what nuget.exe verify does).
                EventId eventId = 0;
                _logger.LogInformation(
                    eventId,
                    ex,
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} due to an exception during signature validation.",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);

                return await RejectAsync(
                    context,
                    new ClientSigningVerificationFailure(ex.Code.ToString(), ex.Message));
            }

            return null;
        }

        private async Task<SignatureValidatorResult> PerformInitialValidationsAsync(Context context)
        {
            // First, detect format errors with a minimal verification. This doesn't even check package integrity. The
            // minimal verification is expected to swallow any sort of signature format exception.
            var invalidFormatResult = await GetVerifyResult(
                context,
                FormatVerificationName,
                _minimalPackageSignatureVerifier);
            if (invalidFormatResult != null)
            {
                return invalidFormatResult;
            }

            // We now know we can safely read the signature.
            context.Signature = await context.PackageReader.GetPrimarySignatureAsync(context.CancellationToken);

            // Only reject counter signatures that have the author commitment type. Repository counter signatures
            // are removed and replaced if they are invalid and valid ones are left as-is. Counter signatures
            // without author or repository signature commitment type are not produced by the client but
            // technically are just fine and are therefore left as-is.
            var authorCounterSignatureCount = context.Signature
                .SignerInfo
                .CounterSignerInfos
                .Cast<SignerInfo>()
                .Select(x => AttributeUtility.GetSignatureType(x.SignedAttributes))
                .Count(signatureType => signatureType == SignatureType.Author);
            if (authorCounterSignatureCount > 0)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has {AuthorCounterSignatureCount} invalid countersignatures.",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    authorCounterSignatureCount);

                return await RejectAsync(
                    context,
                    ValidationIssue.AuthorCounterSignaturesNotSupported);
            }

            return null;
        }

        private async Task<SignatureValidatorResult> StripRepositorySignaturesAsync(Context context)
        {
            Stream packageStreamToDispose = null;
            try
            {
                packageStreamToDispose = FileStreamUtility.GetTemporaryFile();

                var stopwatch = Stopwatch.StartNew();

                var changed = await SignedPackageArchiveUtility.RemoveRepositorySignaturesAsync(
                    context.PackageStream,
                    packageStreamToDispose,
                    context.CancellationToken);

                _telemetryService.TrackDurationToStripRepositorySignatures(
                    stopwatch.Elapsed,
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    changed);

                if (changed)
                {
                    _logger.LogInformation(
                        "Repository signatures were removed from package {PackageId} {PackageVersion} for validation {ValidationId}.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    // The input stream and the input signed package reader are no longer useful since they contain
                    // the removed repository signatures. We need to initialize the new signed package archive
                    // reader and signature.
                    context.PackageReader.Dispose();
                    context.PackageStream.Dispose();

                    context.PackageStream = packageStreamToDispose;
                    context.Changed = true;
                    packageStreamToDispose = null;
                    context.PackageReader = new SignedPackageArchive(context.PackageStream, packageWriteStream: Stream.Null);

                    var initialSignature = context.Signature;

                    if (await context.PackageReader.IsSignedAsync(context.CancellationToken))
                    {
                        _logger.LogInformation(
                            "The package {PackageId} {PackageVersion} for validation {ValidationId} is still signed.",
                            context.Message.PackageId,
                            context.Message.PackageVersion,
                            context.Message.ValidationId);

                        context.Signature = await context.PackageReader.GetPrimarySignatureAsync(context.CancellationToken);

                        _telemetryService.TrackStrippedRepositorySignatures(
                            context.Message.PackageId,
                            context.Message.PackageVersion,
                            context.Message.ValidationId,
                            initialSignature,
                            context.Signature);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "The package {PackageId} {PackageVersion} for validation {ValidationId} is no longer signed.",
                            context.Message.PackageId,
                            context.Message.PackageVersion,
                            context.Message.ValidationId);

                        // The package is now unsigned. This would happen if the primary signature was a repository
                        // signature that was removed.
                        context.Signature = null;

                        _telemetryService.TrackStrippedRepositorySignatures(
                            context.Message.PackageId,
                            context.Message.PackageVersion,
                            context.Message.ValidationId,
                            initialSignature,
                            outputSignature: null);

                        return await HandleUnsignedPackageAsync(context);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No repository signatures were removed from package {PackageId} {PackageVersion} for validation {ValidationId}.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);
                }
            }
            finally
            {
                packageStreamToDispose?.Dispose();
            }

            return null;
        }

        private async Task<SignatureValidatorResult> PerformFinalValidationAsync(Context context)
        {
            if (context.Signature.Type != SignatureType.Author)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it is not author signed: {SignatureType}",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    context.Signature.Type);

                return await RejectAsync(context, ValidationIssue.OnlyAuthorSignaturesSupported);
            }

            // Block packages with any unknown signing certificates.
            var signingCertificate = context.Signature
                .SignerInfo
                .Certificate;
            var signingFingerprint = signingCertificate.ComputeSHA256Thumbprint();

            var packageRegistration = _corePackageService.FindPackageRegistrationById(context.Message.PackageId);

            if (!packageRegistration.IsAcceptableSigningCertificate(signingFingerprint))
            {
                _logger.LogWarning(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has an unknown certificate fingerprint: {UnknownFingerprint}",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    signingFingerprint);

                return await RejectAsync(
                    context,
                    new UnauthorizedCertificateFailure(signingCertificate.Thumbprint.ToLowerInvariant()));
            }

            // Call the "verify" API, which does the main logic of signature validation.
            var failureResult = await GetVerifyResult(
                context,
                SignatureVerificationName,
                _fullPackageSignatureVerifier);
            if (failureResult != null)
            {
                return failureResult;
            }

            _logger.LogInformation(
                "Signed package {PackageId} {PackageVersion} for validation {ValidationId} is valid with certificate fingerprint: {SigningFingerprint}",
                context.Message.PackageId,
                context.Message.PackageVersion,
                context.Message.ValidationId,
                signingFingerprint);

            await _corePackageService.UpdatePackageSigningCertificateAsync(
                context.Message.PackageId,
                context.Message.PackageVersion,
                signingFingerprint);

            return null;
        }

        private async Task<SignatureValidatorResult> GetVerifyResult(
            Context context,
            string verificationName,
            IPackageSignatureVerifier verifier)
        {
            var verifyResult = await verifier.VerifySignaturesAsync(
                context.PackageReader,
                context.CancellationToken,
                parentId: Guid.Empty); // Pass an empty GUID, since we don't use client telemetry infrastructure.

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
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    verificationName,
                    context.Message.ValidationId,
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

                return await RejectAsync(context, errorValidationIssues);
            }
            else
            {
                _logger.LogInformation(
                   "Signed package {PackageId} {PackageVersion} passed {VerificationName} for validation {ValidationId}. Errors: [{Errors}] Warnings: [{Warnings}]",
                   context.Message.PackageId,
                   context.Message.PackageVersion,
                   verificationName,
                   context.Message.ValidationId,
                   errorsForLogs,
                   warningsForLogs);

                return null;
            }
        }

        private async Task<SignatureValidatorResult> RejectAsync(
            Context context,
            params IValidationIssue[] issues)
        {
            await _packageSigningStateService.SetPackageSigningState(
                context.PackageKey,
                context.Message.PackageId,
                context.Message.PackageVersion,
                status: PackageSigningStatus.Invalid);

            return new SignatureValidatorResult(ValidationStatus.Failed, issues, nupkgUri: null);
        }

        private async Task<SignatureValidatorResult> AcceptAsync(
            Context context,
            PackageSigningStatus status)
        {
            await _packageSigningStateService.SetPackageSigningState(
                context.PackageKey,
                context.Message.PackageId,
                context.Message.PackageVersion,
                status);

            // Upload the package stream to storage if the package content has changed.
            Uri nupkgUri = null;
            if (context.Changed)
            {
                await _packageFileService.SaveAsync(
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    context.PackageStream);

                nupkgUri = await _packageFileService.GetReadAndDeleteUriAsync(
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);
            }

            return new SignatureValidatorResult(ValidationStatus.Succeeded, nupkgUri);
        }

        private class Context : IDisposable
        {
            public Context(
                int packageKey,
                Stream packageStream,
                ISignedPackage packageReader,
                SignatureValidationMessage message,
                CancellationToken cancellationToken)
            {
                PackageKey = packageKey;
                PackageStream = packageStream ?? throw new ArgumentNullException(nameof(packageStream));
                PackageReader = packageReader ?? throw new ArgumentNullException(nameof(packageReader));
                Message = message ?? throw new ArgumentNullException(nameof(message));
                CancellationToken = cancellationToken;
            }

            public int PackageKey { get; }
            public bool Changed { get; set; }
            public Stream PackageStream { get; set; }
            public ISignedPackage PackageReader { get; set; }
            public PrimarySignature Signature { get; set; }
            public SignatureValidationMessage Message { get; }
            public CancellationToken CancellationToken { get; }

            public void Dispose()
            {
                PackageStream?.Dispose();
                PackageReader?.Dispose();
            }
        }
    }
}
