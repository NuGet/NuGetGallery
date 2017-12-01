// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public class SignatureValidator : ISignatureValidator
    {
        private readonly IPackageSigningStateService _packageSigningStateService;
        private readonly IEntityRepository<Certificate> _certificates;
        private readonly ILogger<SignatureValidator> _logger;

        public SignatureValidator(
            IPackageSigningStateService packageSigningStateService,
            IEntityRepository<Certificate> certificates,
            ILogger<SignatureValidator> logger)
        {
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ValidateAsync(ISignedPackageReader signedPackageReader, ValidatorStatus validation, SignatureValidationMessage message, CancellationToken cancellationToken)
        {
            if (!await signedPackageReader.IsSignedAsync(cancellationToken))
            {
                await HandleUnsignedPackageAsync(validation, message);
            }
            else
            {
                await HandleSignedPackageAsync(signedPackageReader, validation, message, cancellationToken);
            }
        }
        
        private async Task HandleUnsignedPackageAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary for {ValidationId}.",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId);

            await AcceptAsync(validation, message, PackageSigningStatus.Unsigned);
            return;
        }

        private async Task HandleSignedPackageAsync(
            ISignedPackageReader signedPackageReader,
            ValidatorStatus validation,
            SignatureValidationMessage message,
            CancellationToken cancellationToken)
        {
            // Block packages that don't have exactly one signature.
            var packageSignatures = await signedPackageReader.GetSignaturesAsync(cancellationToken);
            if (packageSignatures.Count != 1)
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has {SignatureCount} signatures.",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId,
                    packageSignatures.Count);

                await RejectAsync(validation, message);
                return;
            }

            // Block packages with any unknown signing certificates.
            var packageThumbprints = GetThumbprints(packageSignatures);
            var knownThumbprints = _certificates
                .GetAll()
                .Where(c => packageThumbprints.Contains(c.Thumbprint))
                .Select(c => c.Thumbprint)
                .ToList();
            
            var unknownThumbprints = packageThumbprints.Except(knownThumbprints);
            if (unknownThumbprints.Any())
            {
                _logger.LogInformation(
                    "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId} since it has unknown certificate thumbprints: {UnknownThumbprints}",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId,
                    unknownThumbprints);

                await RejectAsync(validation, message);
                return;
            }

            // For now, we assume safe-listed certificates are totally valid.
            _logger.LogInformation(
                "Signed package {PackageId} {PackageVersion} is accepted for validation {ValidationId} with certificate thumbprints: {PackageThumbprints}",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId,
                packageThumbprints);

            await AcceptAsync(validation, message, PackageSigningStatus.Valid);
            return;
        }

        private HashSet<string> GetThumbprints(IEnumerable<Signature> signatures)
        {
            var thumbprints = new HashSet<string>();

            foreach (var signature in signatures)
            {
                var certificate = signature.SignerInfo.Certificate;
                using (var sha256 = SHA256.Create())
                {
                    var digestBytes = sha256.ComputeHash(certificate.RawData);
                    var thumbprint = BitConverter
                        .ToString(digestBytes)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();

                    thumbprints.Add(thumbprint);
                }
            }

            return thumbprints;
        }

        private async Task RejectAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            await _packageSigningStateService.SetPackageSigningState(
                validation.PackageKey,
                message.PackageId,
                message.PackageVersion,
                status: PackageSigningStatus.Invalid);

            validation.State = ValidationStatus.Failed;
        }

        private async Task AcceptAsync(ValidatorStatus validation, SignatureValidationMessage message, PackageSigningStatus status)
        {
            await _packageSigningStateService.SetPackageSigningState(
                validation.PackageKey,
                message.PackageId,
                message.PackageVersion,
                status);

            validation.State = ValidationStatus.Succeeded;
        }
    }
}
