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
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class SignatureValidator : ISignatureValidator
    {
        private const string FormatVerificationName = "format verification";
        private const string AuthorSignatureVerificationName = "author signature integrity and trust verification";
        private const string FullSignatureVerificationName = "full signature integrity and trust verification";

        private readonly IPackageSigningStateService _packageSigningStateService;
        private readonly ISignatureFormatValidator _formatValidator;
        private readonly ISignaturePartsExtractor _signaturePartsExtractor;
        private readonly IProcessorPackageFileService _packageFileService;
        private readonly ICorePackageService _corePackageService;
        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _configuration;
        private readonly SasDefinitionConfiguration _sasDefinitionConfiguration;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SignatureValidator> _logger;

        public SignatureValidator(
            IPackageSigningStateService packageSigningStateService,
            ISignatureFormatValidator formatValidator,
            ISignaturePartsExtractor signaturePartsExtractor,
            IProcessorPackageFileService packageFileService,
            ICorePackageService corePackageService,
            IOptionsSnapshot<ProcessSignatureConfiguration> configuration,
            IOptionsSnapshot<SasDefinitionConfiguration> sasDefinitionConfigurationAccessor,
            ITelemetryService telemetryService,
            ILogger<SignatureValidator> logger)
        {
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _formatValidator = formatValidator ?? throw new ArgumentNullException(nameof(formatValidator));
            _signaturePartsExtractor = signaturePartsExtractor ?? throw new ArgumentNullException(nameof(signaturePartsExtractor));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _corePackageService = corePackageService ?? throw new ArgumentNullException(nameof(corePackageService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sasDefinitionConfiguration = (sasDefinitionConfigurationAccessor == null || sasDefinitionConfigurationAccessor.Value == null) ? new SasDefinitionConfiguration() : sasDefinitionConfigurationAccessor.Value;
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

                SignatureValidatorResult result;

                if (await context.PackageReader.IsSignedAsync(cancellationToken))
                {
                    result = await HandleSignedPackageAsync(context);
                }
                else
                {
                    result = await HandleUnsignedPackageAsync(context);
                }

                // Force the validation to fail if the repository signature is expected but missing. The signature
                // and signing state that are stored in the database may be still valid.
                if (context.Message.RequireRepositorySignature && !context.HasRepositorySignature)
                {
                    _logger.LogCritical(
                        "Package {PackageId} {PackageVersion} for validation {ValidationId} is expected to be repository signed.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    return new SignatureValidatorResult(ValidationStatus.Failed, result.Issues, nupkgUri: null);
                }

                return result;
            }
        }

        private async Task<SignatureValidatorResult> HandleUnsignedPackageAsync(Context context)
        {
            var validationResult = await ValidatePackageRegistrationSigningRequirementsAsync(context);
            if (validationResult != null)
            {
                return validationResult;
            }

            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary for " +
                "{ValidationId}.",
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

                // Strip repository signatures that don't match the allow list from configuration.
                var stripRepositorySignaturesResult = await StripUnacceptableRepositorySignaturesAsync(context);
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
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} due to an " +
                    "exception during signature validation.",
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
            var verifyResult = await _formatValidator.ValidateMinimalAsync(
                context.PackageReader,
                context.CancellationToken);
            var invalidFormatResult = await GetVerifyResult(
                context,
                FormatVerificationName,
                verifyResult);
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
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it " +
                    "has {AuthorCounterSignatureCount} invalid countersignatures.",
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

        private async Task<SignatureValidatorResult> StripUnacceptableRepositorySignaturesAsync(Context context)
        {
            // Check if the repository signing certificates are acceptable.
            if (await HasAllValidRepositorySignaturesAsync(context))
            {
                _logger.LogInformation(
                    "No repository signatures needed removal from package {PackageId} {PackageVersion} for " +
                    "validation {ValidationId}.",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);

                return null;
            }

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
                        "Repository signatures were removed from package {PackageId} {PackageVersion} for " +
                        "validation {ValidationId}.",
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

        private async Task<bool> HasAllValidRepositorySignaturesAsync(Context context)
        {
            if (context.Signature.Type == SignatureType.Repository)
            {
                if (!await IsValidRepositorySignatureAsync(context, (RepositoryPrimarySignature)context.Signature))
                {
                    _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has repository " +
                        "primary signature that is invalid.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    return false;
                }
            }

            try
            {
                var repositoryCounterSignature = RepositoryCountersignature.GetRepositoryCountersignature(context.Signature);

                if (repositoryCounterSignature != null
                    && !await IsValidRepositorySignatureAsync(context, repositoryCounterSignature))
                {
                    _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has repository " +
                        "countersignature that is invalid.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    return false;
                }
            }
            catch (SignatureException ex)
            {
                // This handles the case when there are multiple repository signatures.
                _logger.LogInformation(
                    0,
                    ex,
                    "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has repository " +
                    "countersignature that is invalid due to an exception.",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);

                return false;
            }

            return true;
        }

        private async Task<bool> IsValidRepositorySignatureAsync<T>(Context context, T signature)
            where T : Signature, IRepositorySignature
        {
            // Strip repository signatures that do not match the configurations.
            if (signature.V3ServiceIndexUrl?.AbsoluteUri != _configuration.Value.V3ServiceIndexUrl)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has invalid V3 index " +
                    "URL {V3ServiceIndexUrl} in the repository signature",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    signature.V3ServiceIndexUrl?.AbsoluteUri);

                return false;
            }

            var fingerprint = signature.SignerInfo.Certificate.ComputeSHA256Thumbprint();

            if (!_configuration.Value.AllowedRepositorySigningCertificates.Contains(fingerprint, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has unacceptable " +
                    "signing certificate {Fingerprint} for the repository signature",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    fingerprint);

                return false;
            }

            // Strip repository signatures that do not pass verification.
            var verifyResult = await _formatValidator.ValidateRepositorySignatureAsync(context.PackageReader, context.CancellationToken);

            if (!verifyResult.IsValid)
            {
                var warningsForLogs = verifyResult
                    .Results
                    .SelectMany(x => x.GetWarningIssues())
                    .Select(x => $"{x.Code}: {x.Message}")
                    .ToList();
                var errorsForLogs = verifyResult
                    .Results
                    .SelectMany(x => x.GetErrorIssues())
                    .Select(x => $"{x.Code}: {x.Message}")
                    .ToList();

                // The repository signature matches our service's configuration but did not pass verification. This could mean that:
                //
                // 1. The customer attempted to forge our repository signature on a newly uploaded package
                // 2. The customer downloaded a repository signed package, modified the package, and reuploaded it
                // 3. The customer downloaded a repository signed package, reuploaded it, and the package failed trust verification
                // 4. We repository signed this package and our repository signature does not pass trust or integrity verification
                //
                // For cases #1 and #2, we can strip the repository signature and apply a new one. Cases #3 and #4 are highly suspicious
                // and an on-call engineer should investigate.
                if (await _packageSigningStateService.HasPackageSigningStateAsync(context.PackageKey))
                {
                    // This package failed verification and has a PackageSigningState. This is case #4 as
                    // we extract the package's signing state before we repository sign packages.
                    _logger.LogCritical(
                        "Package {PackageId} {PackageVersion} was repository signed with a signature that fails verification on " +
                        "validation {ValidationId}. Errors: {Errors} Warnings: {Warnings}",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId,
                        errorsForLogs,
                        warningsForLogs);

                    throw new InvalidOperationException(
                        $"Package was repository signed with a signature that fails verification for validation id '{context.Message.ValidationId}'");
                }

                // For all other cases, strip the repository signature so that a new repository signature will be applied. Note that for
                // case #3, the newly applied repository signature may still fail verification, thus triggering case #4.
                _logger.LogInformation(
                    "Repository signature failed verification and will be stripped for package {PackageId} and {PackageVersion} on validation {ValidationId}. " +
                    "Errors: {Errors} Warnings: {Warnings}",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    errorsForLogs,
                    warningsForLogs);

                return false;
            }

            _logger.LogInformation(
                "Signed package {PackageId} {PackageVersion} for validation {ValidationId} has a valid repository signature",
                context.Message.PackageId,
                context.Message.PackageVersion,
                context.Message.ValidationId);

            // If configured, strip valid repository signatures from packages to force a new repository signature to be applied.
            if (_configuration.Value.StripValidRepositorySignatures)
            {
                // Packages' signatures are validated twice: once before the package is repository signed, and once after. We do not
                // want to strip the newly applied repository signature, so we will only strip in the "before" case. We can detect the
                // "after" case as it requires the presence of a repository signature.
                if (!context.Message.RequireRepositorySignature)
                {
                    _logger.LogWarning(
                        $"The {nameof(ProcessSignatureConfiguration.StripValidRepositorySignatures)} configuration is enabled, " +
                        "stripping the valid repository signature from package {PackageId} {PackageVersion} for validation {ValidationId}.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    return false;
                }
            }

            return true;
        }

        private async Task<SignatureValidatorResult> PerformFinalValidationAsync(Context context)
        {
            var packageRegistration = _corePackageService.FindPackageRegistrationById(context.Message.PackageId);

            // Ensure the signature matches the package registration's signing requirements.
            var validationResult = await ValidatePackageRegistrationSigningRequirementsAsync(context);
            if (validationResult != null)
            {
                return validationResult;
            }

            if (context.Signature.Type != SignatureType.Author &&
                context.Signature.Type != SignatureType.Repository)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it " +
                    "is neither author nor repository signed: {SignatureType}",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId,
                    context.Signature.Type);

                return await RejectAsync(context, ValidationIssue.OnlyAuthorSignaturesSupported);
            }

            // If the package has both an author and repository signature, verify the author signature independently.
            if (context.HasAuthorSignature && context.HasRepositorySignature)
            {
                var authorVerificationResult = await _formatValidator.ValidateAuthorSignatureAsync(
                    context.PackageReader,
                    context.CancellationToken);
                var authorFailureResult = await GetVerifyResult(
                    context,
                    AuthorSignatureVerificationName,
                    authorVerificationResult);

                if (authorFailureResult != null)
                {
                    return authorFailureResult;
                }
            }

            // Do a full verification of all signatures.
            var signingCertificate = context.Signature.SignerInfo.Certificate;
            var signingFingerprint = signingCertificate.ComputeSHA256Thumbprint();

            var verifyResult = await _formatValidator.ValidateAllSignaturesAsync(
                context.PackageReader,
                context.HasRepositorySignature,
                context.CancellationToken);
            var failureResult = await GetVerifyResult(
                context,
                FullSignatureVerificationName,
                verifyResult);
            if (failureResult != null)
            {
                return failureResult;
            }

            _logger.LogInformation(
                "{SignatureTyped} signed package {PackageId} {PackageVersion} for validation {ValidationId} is valid" +
                " with certificate fingerprint: {SigningFingerprint}",
                context.Signature.Type,
                context.Message.PackageId,
                context.Message.PackageVersion,
                context.Message.ValidationId,
                signingFingerprint);

            if (context.Signature.Type == SignatureType.Author)
            {
                await _corePackageService.UpdatePackageSigningCertificateAsync(
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    signingFingerprint);
            }

            return null;
        }

        private async Task<SignatureValidatorResult> ValidatePackageRegistrationSigningRequirementsAsync(Context context)
        {
            // Skip signing requirement checks if the package is already available. This is needed otherwise revalidating
            // a package after its owners have changed their signing requirements may fail.
            var package = _corePackageService.FindPackageByIdAndVersionStrict(context.Message.PackageId, context.Message.PackageVersion);

            if (package.PackageStatusKey == PackageStatus.Available)
            {
                _logger.LogInformation(
                    "Package {PackageId} {PackageVersion} for validation {ValidationId} is already available, " +
                    "skipping the package registration's certificate signing requirements",
                    context.Message.PackageId,
                    context.Message.PackageVersion,
                    context.Message.ValidationId);

                return null;
            }

            var packageRegistration = _corePackageService.FindPackageRegistrationById(context.Message.PackageId);

            if (context.Signature == null || context.Signature.Type == SignatureType.Repository)
            {
                // Block unsigned packages if the registration requires a signature.
                if (packageRegistration.IsSigningRequired())
                {
                    _logger.LogWarning(
                        "Package {PackageId} {PackageVersion} for validation {ValidationId} must be signed but is unsigned.",
                        context.Message.PackageId,
                        context.Message.PackageVersion,
                        context.Message.ValidationId);

                    return await RejectAsync(context, ValidationIssue.PackageIsNotSigned);
                }
            }

            if (context.Signature?.Type == SignatureType.Author)
            {
                var signingCertificate = context.Signature.SignerInfo.Certificate;
                var signingFingerprint = signingCertificate.ComputeSHA256Thumbprint();

                // Block packages with any unknown signing certificates.
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
            }

            return null;
        }

        private async Task<SignatureValidatorResult> GetVerifyResult(
            Context context,
            string verificationName,
            VerifySignaturesResult verifyResult)
        {
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

            if (!verifyResult.IsValid)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked during {VerificationName} for validation " +
                    "{ValidationId}. Errors: {Errors} Warnings: {Warnings}",
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
                   "Signed package {PackageId} {PackageVersion} passed {VerificationName} for validation " +
                   "{ValidationId}. Errors: {Errors} Warnings: {Warnings}",
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
                    context.Message.ValidationId,
                    _sasDefinitionConfiguration.SignatureValidatorSasDefinition);
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

            public bool HasAuthorSignature => Signature?.Type == SignatureType.Author;

            public bool HasRepositorySignature
            {
                get
                {
                    if (Signature == null)
                    {
                        return false;
                    }

                    if (Signature.Type == SignatureType.Repository)
                    {
                        return true;
                    }

                    return SignatureUtility.HasRepositoryCountersignature(Signature);
                }
            }

            public void Dispose()
            {
                PackageStream?.Dispose();
                PackageReader?.Dispose();
            }
        }
    }
}
