// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class SignaturePartsExtractor : ISignaturePartsExtractor
    {
        private readonly ICertificateStore _certificateStore;
        private readonly IValidationEntitiesContext _entitiesContext;
        private readonly ILogger<SignaturePartsExtractor> _logger;

        public SignaturePartsExtractor(
            ICertificateStore certificateStore,
            IValidationEntitiesContext entitiesContext,
            ILogger<SignaturePartsExtractor> logger)
        {
            _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExtractAsync(int packageKey, ISignedPackageReader signedPackageReader, CancellationToken token)
        {
            if (!await signedPackageReader.IsSignedAsync(token))
            {
                throw new ArgumentException("The provided package reader must refer to a signed package.", nameof(signedPackageReader));
            }

            // Read the package signature.
            var signature = await signedPackageReader.GetPrimarySignatureAsync(token);

            // Extract the certificates found in the package signatures.
            var extractedCertificates = ExtractCertificates(signature);

            // Prepare signature entities for the database (does not commit).
            await SaveSignatureToDatabaseAsync(packageKey, signature, extractedCertificates);

            // Save the certificates to blob storage.
            await SaveCertificatesToStoreAsync(extractedCertificates, token);

            // Commit the database changes.
            await _entitiesContext.SaveChangesAsync();
        }

        private ExtractedCertificates ExtractCertificates(PrimarySignature signature)
        {
            if (signature.Timestamps.Count != 1)
            {
                throw new ArgumentException("There should be exactly one timestamp.", nameof(signature));
            }

            var signatureCertificates = SignatureUtility
                .GetPrimarySignatureCertificates(signature);
            if (signatureCertificates == null || !signatureCertificates.Any())
            {
                throw new ArgumentException(
                    "The provided signature must have at least one primary signing certificate.",
                    nameof(signature));
            }

            var hashedSignatureCertificates = signatureCertificates
                .Select(x => new HashedCertificate(x))
                .ToList();
            var signatureEndCertificate = hashedSignatureCertificates.First();
            var signatureParentCertificates = hashedSignatureCertificates.Skip(1).ToList();

            var timestampCertificates = SignatureUtility
                .GetPrimarySignatureTimestampCertificates(signature);
            if (timestampCertificates == null || !timestampCertificates.Any())
            {
                throw new ArgumentException(
                    "The provided signature must have at least one timestamp certificate.",
                    nameof(signature));
            }

            var hashedTimestampCertificates = timestampCertificates
                .Select(x => new HashedCertificate(x))
                .ToList();
            var timestampEndCertificate = hashedTimestampCertificates.First();
            var timestampParentCertificates = hashedTimestampCertificates.Skip(1).ToList();

            return new ExtractedCertificates(
                signatureEndCertificate,
                signatureParentCertificates,
                timestampEndCertificate,
                timestampParentCertificates);
        }

        private async Task SaveSignatureToDatabaseAsync(int packageKey, Signature signature, ExtractedCertificates extractedCertificates)
        {
            // Initialize the end and parent certificates.
            var thumbprintToEndCertificate = await InitializeEndCertificatesAsync(
                new[]
                {
                    new CertificateAndUse(extractedCertificates.SignatureEndCertificate, EndCertificateUse.CodeSigning),
                    new CertificateAndUse(extractedCertificates.TimestampEndCertificate, EndCertificateUse.Timestamping),
                });

            var thumbprintToParentCertificate = await InitializeParentCertificatesAsync(
                extractedCertificates
                    .SignatureParentCertificates
                    .Concat(extractedCertificates.TimestampParentCertificates));

            // Connect the end and parent certificates.
            ConnectCertificates(
                extractedCertificates.SignatureEndCertificate,
                extractedCertificates.SignatureParentCertificates,
                thumbprintToEndCertificate,
                thumbprintToParentCertificate);

            ConnectCertificates(
                extractedCertificates.TimestampEndCertificate,
                extractedCertificates.TimestampParentCertificates,
                thumbprintToEndCertificate,
                thumbprintToParentCertificate);

            // Initialize the package signature record.
            var packageSignature = await InitializePackageSignatureAsync(
                packageKey,
                extractedCertificates.SignatureEndCertificate,
                thumbprintToEndCertificate);

            // Initialize the trusted timestamp record.
            InitializeTrustedTimestamp(
                packageSignature,
                signature,
                extractedCertificates.TimestampEndCertificate,
                thumbprintToEndCertificate);
        }

        public async Task<PackageSignature> InitializePackageSignatureAsync(
            int packageKey,
            HashedCertificate signatureEndCertificate,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate)
        {
            var packageSignatures = await _entitiesContext
                .PackageSignatures
                .Include(x => x.TrustedTimestamps)
                .Include(x => x.EndCertificate)
                .Where(x => x.PackageKey == packageKey)
                .ToListAsync();

            if (packageSignatures.Count > 1)
            {
                _logger.LogError(
                    "There are {Count} package signatures for package key {PackageKey}. There should be either zero or one.",
                    packageSignatures.Count,
                    packageKey);

                throw new InvalidOperationException("There should never be more than one package signature per package.");
            }

            PackageSignature packageSignature;
            if (packageSignatures.Count == 0)
            {
                packageSignature = new PackageSignature
                {
                    CreatedAt = DateTime.UtcNow,
                    EndCertificate = thumbprintToEndCertificate[signatureEndCertificate.Thumbprint],
                    PackageKey = packageKey,
                    Status = PackageSignatureStatus.Unknown,
                    TrustedTimestamps = new List<TrustedTimestamp>(),
                };
                _entitiesContext.PackageSignatures.Add(packageSignature);

                packageSignature.EndCertificateKey = packageSignature.EndCertificate.Key;
            }
            else
            {
                packageSignature = packageSignatures.Single();

                if (packageSignature.EndCertificate.Thumbprint != signatureEndCertificate.Thumbprint)
                {
                    _logger.LogError(
                        "The signature end certificate thumbprint cannot change for package {PackageKey}. The " +
                        "existing signature end certificate is {ExistingThumbprint}. The new thumprint is " +
                        "{NewThumbprint}.",
                        packageKey,
                        packageSignature.EndCertificate.Thumbprint,
                        signatureEndCertificate.Thumbprint);

                    throw new InvalidOperationException("The thumbprint of the signature end certificate cannot change.");
                }
            }

            return packageSignature;
        }

        private void InitializeTrustedTimestamp(
            PackageSignature packageSignature,
            Signature signature,
            HashedCertificate timestampEndCertificate,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate)
        {
            if (packageSignature.TrustedTimestamps.Count > 1)
            {
                _logger.LogError(
                    "There are {Count} trusted timestamps for signature on package {PackageKey}. There should be either zero or one.",
                    packageSignature.TrustedTimestamps.Count,
                    packageSignature.PackageKey);

                throw new InvalidOperationException("There should never be more than one trusted timestamp per package signature.");
            }

            // Determine the value of the timestamp.
            var value = signature.Timestamps.Single().UpperLimit.UtcDateTime;

            TrustedTimestamp trustedTimestamp;
            if (packageSignature.TrustedTimestamps.Count == 0)
            {
                trustedTimestamp = new TrustedTimestamp
                {
                    PackageSignature = packageSignature,
                    PackageSignatureKey = packageSignature.Key,
                    EndCertificate = thumbprintToEndCertificate[timestampEndCertificate.Thumbprint],
                    Value = value,
                    Status = TrustedTimestampStatus.Valid,
                };
                trustedTimestamp.EndCertificateKey = trustedTimestamp.EndCertificate.Key;
                packageSignature.TrustedTimestamps.Add(trustedTimestamp);
                _entitiesContext.TrustedTimestamps.Add(trustedTimestamp);
            }
            else
            {
                trustedTimestamp = packageSignature.TrustedTimestamps.Single();

                if (trustedTimestamp.EndCertificate.Thumbprint != timestampEndCertificate.Thumbprint)
                {
                    _logger.LogError(
                        "The timestamp end certificate thumbprint cannot change for package {PackageKey}. The " +
                        "existing timestamp end certificate is {ExistingThumbprint}. The new thumprint is " +
                        "{NewThumbprint}.",
                        packageSignature.PackageKey,
                        packageSignature.EndCertificate.Thumbprint,
                        timestampEndCertificate.Thumbprint);

                    throw new InvalidOperationException("The thumbprint of the timestamp end certificate cannot change.");
                }

                if (trustedTimestamp.Value != value)
                {
                    _logger.LogError(
                        "The trusted timestamp value cannot change for package {PackageKey}. The existing timestamp " +
                        "value is {ExistingValue}. The new value is {NewValue}.",
                        packageSignature.PackageKey,
                        trustedTimestamp.Value,
                        value);

                    throw new InvalidOperationException("The value of the trusted timestamp cannot change.");
                }
            }
        }

        private void ConnectCertificates(
            HashedCertificate endCertificate,
            IReadOnlyList<HashedCertificate> parentCertificates,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate,
            IReadOnlyDictionary<string, ParentCertificate> thumbprintToParentCertificates)
        {
            var endCertificateEntity = thumbprintToEndCertificate[endCertificate.Thumbprint];
            var parentCertificateKeys = new HashSet<long>(endCertificateEntity
                .CertificateChainLinks
                .Select(x => x.ParentCertificateKey));

            foreach (var parentCertificate in parentCertificates)
            {
                var parentCertificateEntity = thumbprintToParentCertificates[parentCertificate.Thumbprint];

                // If either end of the link is new, the link must be new.
                if (endCertificateEntity.Key == default(long)
                    || parentCertificateEntity.Key == default(long)
                    || !parentCertificateKeys.Contains(parentCertificateEntity.Key))
                {
                    var link = new CertificateChainLink
                    {
                        EndCertificate = endCertificateEntity,
                        ParentCertificate = parentCertificateEntity,
                    };
                    _entitiesContext.CertificateChainLinks.Add(link);
                    endCertificateEntity.CertificateChainLinks.Add(link);
                    parentCertificateEntity.CertificateChainLinks.Add(link);

                    if (parentCertificateEntity.Key != default(long))
                    {
                        parentCertificateKeys.Add(parentCertificateEntity.Key);
                    }
                }
            }
        }

        private async Task<IReadOnlyDictionary<string, EndCertificate>> InitializeEndCertificatesAsync(
            IEnumerable<CertificateAndUse> certificatesAndUses)
        {
            var thumbprints = certificatesAndUses
                .Select(x => x.Certificate.Thumbprint)
                .Distinct()
                .ToList();

            // Find all of the end certificate entities that intersect with the set of certificates found in the
            // package that is currently being processed.
            var existingEntities = await _entitiesContext
                .EndCertificates
                .Include(x => x.CertificateChainLinks)
                .Where(x => thumbprints.Contains(x.Thumbprint))
                .ToListAsync();
            
            var thumbprintToEntity = existingEntities.ToDictionary(x => x.Thumbprint);

            foreach (var certificateAndUse in certificatesAndUses)
            {
                if (!thumbprintToEntity.TryGetValue(certificateAndUse.Certificate.Thumbprint, out var entity))
                {
                    entity = new EndCertificate
                    {
                        Status = EndCertificateStatus.Unknown,
                        Use = certificateAndUse.Use,
                        Thumbprint = certificateAndUse.Certificate.Thumbprint,
                        CertificateChainLinks = new List<CertificateChainLink>(),
                    };
                    _entitiesContext.EndCertificates.Add(entity);

                    thumbprintToEntity[certificateAndUse.Certificate.Thumbprint] = entity;
                }
                else if (entity.Use != certificateAndUse.Use)
                {
                    _logger.LogError(
                        "The use of end certificate {Thumbprint} cannot change. The existing use is {ExistingUse}. The new use is {NewUse}.",
                        certificateAndUse.Certificate.Thumbprint,
                        entity.Use,
                        certificateAndUse.Use);

                    throw new InvalidOperationException("The use of an end certificate cannot change.");
                }
            }

            return thumbprintToEntity;
        }

        private async Task<IReadOnlyDictionary<string, ParentCertificate>> InitializeParentCertificatesAsync(
            IEnumerable<HashedCertificate> certificates)
        {
            var thumbprints = certificates
                .Select(x => x.Thumbprint)
                .Distinct()
                .ToList();

            // Find all of the parent certificate entities that intersect with the set of certificates found in the
            // package that is currently being processed.
            var existingEntities = await _entitiesContext
                .ParentCertificates
                .Include(x => x.CertificateChainLinks)
                .Where(x => thumbprints.Contains(x.Thumbprint))
                .ToListAsync();
            
            var thumbprintToEntity = existingEntities.ToDictionary(x => x.Thumbprint);

            foreach (var certificate in certificates)
            {
                if (!thumbprintToEntity.TryGetValue(certificate.Thumbprint, out var entity))
                {
                    entity = new ParentCertificate
                    {
                        Thumbprint = certificate.Thumbprint,
                        CertificateChainLinks = new List<CertificateChainLink>(),
                    };
                    _entitiesContext.ParentCertificates.Add(entity);

                    thumbprintToEntity[certificate.Thumbprint] = entity;
                }
            }

            return thumbprintToEntity;
        }

        private async Task SaveCertificatesToStoreAsync(ExtractedCertificates extractedCertificates, CancellationToken token)
        {
            var allCertificates = Enumerable
                .Empty<HashedCertificate>()
                .Concat(new[] { extractedCertificates.SignatureEndCertificate })
                .Concat(extractedCertificates.SignatureParentCertificates)
                .Concat(new[] { extractedCertificates.TimestampEndCertificate })
                .Concat(extractedCertificates.TimestampParentCertificates);

            foreach (var certificate in allCertificates)
            {
                await SaveCertificateToStoreAsync(certificate, token);
            }
        }

        private async Task SaveCertificateToStoreAsync(HashedCertificate certificate, CancellationToken token)
        {
            if (await _certificateStore.ExistsAsync(certificate.Thumbprint, token))
            {
                return;
            }

            await _certificateStore.SaveAsync(certificate.Certificate, token);
        }

        private class CertificateAndUse
        {
            public CertificateAndUse(HashedCertificate hashedCertificate, EndCertificateUse endCertificateUse)
            {
                Certificate = hashedCertificate;
                Use = endCertificateUse;
            }

            public HashedCertificate Certificate { get; }
            public EndCertificateUse Use { get; }
        }
    }
}
