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

        public async Task ExtractAsync(int packageKey, PrimarySignature primarySignature, CancellationToken cancellationToken)
        {
            using (var context = new Context(packageKey, primarySignature, cancellationToken))
            {
                if (primarySignature == null)
                {
                    throw new ArgumentNullException(nameof(primarySignature));
                }

                // Extract the certificates found in the package signatures.
                ExtractSignaturesAndCertificates(context);

                // Prepare signature and certificate entities for the database but don't commit.
                await PrepareSignaturesAndCertificatesRecordsAsync(context);

                // Save the certificates to blob storage.
                await SaveCertificatesToStoreAsync(context);

                // Commit the database changes.
                await _entitiesContext.SaveChangesAsync();
            }
        }

        private static void ExtractSignaturesAndCertificates(Context context)
        {
            if (context.PrimarySignature.Timestamps.Count != 1)
            {
                throw new InvalidOperationException("There should be exactly one timestamp on the primary signature.");
            }

            var primarySignatureCertificates = ExtractPrimarySignatureCertificates(context);

            if (context.PrimarySignature.Type == SignatureType.Author)
            {
                context.Author = new SignatureAndCertificates(context.PrimarySignature, primarySignatureCertificates);

                var repositoryCountersignature = RepositoryCountersignature.GetRepositoryCountersignature(context.PrimarySignature);
                if (repositoryCountersignature != null)
                {
                    if (repositoryCountersignature.Timestamps.Count != 1)
                    {
                        throw new InvalidOperationException("There should be exactly one timestamp on the repository countersignature.");
                    }

                    var countersignatureCertificates = ExtractRepositoryCountersignatureCertificates(context, repositoryCountersignature);
                    context.Repository = new SignatureAndCertificates(repositoryCountersignature, countersignatureCertificates);
                }
            }
            else if (context.PrimarySignature.Type == SignatureType.Repository)
            {
                context.Repository = new SignatureAndCertificates(context.PrimarySignature, primarySignatureCertificates);
            }
            else
            {
                throw new InvalidOperationException("The primary signature must be an author or repository signature.");
            }
        }

        private static ExtractedCertificates ExtractPrimarySignatureCertificates(Context context)
        {
            return ExtractCertificates(context, repositoryCountersignature: null);
        }

        private static ExtractedCertificates ExtractRepositoryCountersignatureCertificates(
            Context context,
            RepositoryCountersignature repositoryCountersignature)
        {
            return ExtractCertificates(context, repositoryCountersignature);
        }

        private static ExtractedCertificates ExtractCertificates(
            Context context,
            RepositoryCountersignature repositoryCountersignature)
        {
            IX509CertificateChain signatureCertificates;
            if (repositoryCountersignature == null)
            {
                signatureCertificates = SignatureUtility.GetCertificateChain(context.PrimarySignature);
            }
            else
            {
                signatureCertificates = SignatureUtility.GetCertificateChain(context.PrimarySignature, repositoryCountersignature);
            }

            context.Disposables.Add(signatureCertificates);

            if (signatureCertificates == null || !signatureCertificates.Any())
            {
                throw new InvalidOperationException("The provided signature must have at least one signing certificate.");
            }

            var hashedSignatureCertificates = signatureCertificates
                .Select(x => new HashedCertificate(x))
                .ToList();
            var signatureEndCertificate = hashedSignatureCertificates.First();
            var signatureParentCertificates = hashedSignatureCertificates.Skip(1).ToList();

            IX509CertificateChain timestampCertificates;
            if (repositoryCountersignature == null)
            {
                timestampCertificates = SignatureUtility.GetTimestampCertificateChain(context.PrimarySignature);
            }
            else
            {
                timestampCertificates = SignatureUtility.GetTimestampCertificateChain(context.PrimarySignature, repositoryCountersignature);
            }

            context.Disposables.Add(timestampCertificates);

            if (timestampCertificates == null || !timestampCertificates.Any())
            {
                throw new InvalidOperationException("The provided signature must have at least one timestamp certificate.");
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

        private async Task PrepareSignaturesAndCertificatesRecordsAsync(Context context)
        {
            // Initialize the end and parent certificates.
            var endCertificatesAndUses = new List<EndCertificateAndUse>();
            var parentCertificates = new List<HashedCertificate>();

            CollectCertificates(endCertificatesAndUses, parentCertificates, context.Author?.Certificates);
            CollectCertificates(endCertificatesAndUses, parentCertificates, context.Repository?.Certificates);

            var thumbprintToEndCertificate = await InitializeEndCertificatesAsync(endCertificatesAndUses);
            var thumbprintToParentCertificate = await InitializeParentCertificatesAsync(parentCertificates);

            // Connect the end and parent certificates.
            ConnectCertificates(context.Author?.Certificates, thumbprintToEndCertificate, thumbprintToParentCertificate);
            ConnectCertificates(context.Repository?.Certificates, thumbprintToEndCertificate, thumbprintToParentCertificate);

            // Initialize the package signature for the author signature. If the record is already in the database,
            // verify that nothing has changed.
            await InitializePackageSignatureAndTrustedTimestampAsync(
                context.PackageKey,
                PackageSignatureType.Author,
                context.Author,
                thumbprintToEndCertificate,
                allowSignatureChanges: false);

            // Initialize the package signature for the repository signature. If the record is already in the database
            // and different than the current repository signature, replace the old one with the new one.
            await InitializePackageSignatureAndTrustedTimestampAsync(
                context.PackageKey,
                PackageSignatureType.Repository,
                context.Repository,
                thumbprintToEndCertificate,
                allowSignatureChanges: true);
        }

        private void ConnectCertificates(
            ExtractedCertificates extractedCertificates,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate,
            IReadOnlyDictionary<string, ParentCertificate> thumbprintToParentCertificate)
        {
            if (extractedCertificates == null)
            {
                return;
            }

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
        }

        private async Task InitializePackageSignatureAndTrustedTimestampAsync(
            int packageKey,
            PackageSignatureType type,
            SignatureAndCertificates signatureAndCertificates,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate,
            bool allowSignatureChanges)
        {
            if (signatureAndCertificates == null)
            {
                return;
            }

            // Initialize the package signature record.
            var packageSignature = await InitializePackageSignatureAsync(
                packageKey,
                type,
                signatureAndCertificates.Certificates.SignatureEndCertificate,
                thumbprintToEndCertificate,
                allowSignatureChanges);

            // Initialize the trusted timestamp record.
            InitializeTrustedTimestamp(
                packageSignature,
                signatureAndCertificates.Signature,
                signatureAndCertificates.Certificates.TimestampEndCertificate,
                thumbprintToEndCertificate);
        }

        private async Task<PackageSignature> InitializePackageSignatureAsync(
            int packageKey,
            PackageSignatureType type,
            HashedCertificate signatureEndCertificate,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate,
            bool replacePackageSignature)
        {
            var packageSignatures = await _entitiesContext
                .PackageSignatures
                .Include(x => x.TrustedTimestamps)
                .Include(x => x.EndCertificate)
                .Where(x => x.PackageKey == packageKey && x.Type == type)
                .ToListAsync();

            if (packageSignatures.Count > 1)
            {
                _logger.LogError(
                    "There are {Count} package signatures for package key {PackageKey} and type {Type}. There should be either zero or one.",
                    packageSignatures.Count,
                    packageKey,
                    type);

                throw new InvalidOperationException("There should never be more than one package signature per package and signature type.");
            }

            PackageSignature packageSignature;
            if (packageSignatures.Count == 0)
            {
                packageSignature = InitializePackageSignature(
                    packageKey,
                    type,
                    signatureEndCertificate,
                    thumbprintToEndCertificate);
            }
            else
            {
                packageSignature = packageSignatures.Single();

                if (packageSignature.EndCertificate.Thumbprint != signatureEndCertificate.Thumbprint)
                {
                    if (replacePackageSignature)
                    {
                        _logger.LogWarning(
                            "The signature end certificate thumbprint has changed for package {PackageKey} and type " +
                            "{Type}. The previous signature end certificate is {ExistingThumbprint}. The new thumprint " +
                            "is {NewThumbprint}. The previous record with key {PackageSignatureKey} will be removed.",
                            packageKey,
                            type,
                            packageSignature.EndCertificate.Thumbprint,
                            signatureEndCertificate.Thumbprint,
                            packageSignature.Key);

                        // Remove the child trusted timestamps. This should be handled by cascading delete but to be
                        // explicit and to facilitate unit testing, we explicitly remove them.
                        foreach (var trustedTimestamp in packageSignature.TrustedTimestamps)
                        {
                            _entitiesContext.TrustedTimestamps.Remove(trustedTimestamp);
                        }

                        _entitiesContext.PackageSignatures.Remove(packageSignature);

                        packageSignature = InitializePackageSignature(
                            packageKey,
                            type,
                            signatureEndCertificate,
                            thumbprintToEndCertificate);
                    }
                    else
                    {
                        _logger.LogError(
                            "The signature end certificate thumbprint cannot change for package {PackageKey} and type " +
                            "{Type}. The existing signature end certificate is {ExistingThumbprint}. The new thumprint " +
                            "is {NewThumbprint}.",
                            packageKey,
                            type,
                            packageSignature.EndCertificate.Thumbprint,
                            signatureEndCertificate.Thumbprint);

                        throw new InvalidOperationException("The thumbprint of the signature end certificate cannot change.");
                    }
                }
            }

            return packageSignature;
        }

        private PackageSignature InitializePackageSignature(
            int packageKey,
            PackageSignatureType type,
            HashedCertificate signatureEndCertificate,
            IReadOnlyDictionary<string, EndCertificate> thumbprintToEndCertificate)
        {
            var packageSignature = new PackageSignature
            {
                CreatedAt = DateTime.UtcNow,
                EndCertificate = thumbprintToEndCertificate[signatureEndCertificate.Thumbprint],
                PackageKey = packageKey,
                Status = PackageSignatureStatus.Unknown,
                Type = type,
                TrustedTimestamps = new List<TrustedTimestamp>(),
            };

            packageSignature.EndCertificateKey = packageSignature.EndCertificate.Key;
            _entitiesContext.PackageSignatures.Add(packageSignature);

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
                    "There are {Count} trusted timestamps for the {SignatureType} signature on package {PackageKey}. There should be either zero or one.",
                    packageSignature.TrustedTimestamps.Count,
                    signature.Type,
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
                        "The timestamp end certificate thumbprint cannot change for the {SignatureType} signature " +
                        "on package {PackageKey}. The existing timestamp end certificate is {ExistingThumbprint}. " +
                        "The new thumprint is {NewThumbprint}.",
                        signature.Type,
                        packageSignature.PackageKey,
                        packageSignature.EndCertificate.Thumbprint,
                        timestampEndCertificate.Thumbprint);

                    throw new InvalidOperationException("The thumbprint of the timestamp end certificate cannot change.");
                }

                if (trustedTimestamp.Value != value)
                {
                    _logger.LogError(
                        "The trusted timestamp value cannot change for the {SignatureType} signature on package " +
                        "{PackageKey}. The existing timestamp value is {ExistingValue}. The new value is {NewValue}.",
                        signature.Type,
                        packageSignature.PackageKey,
                        trustedTimestamp.Value,
                        value);

                    throw new InvalidOperationException("The value of the trusted timestamp cannot change.");
                }
            }
        }

        private void ConnectCertificates(
            HashedCertificate endCertificate,
            IReadOnlyCollection<HashedCertificate> parentCertificates,
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
            IReadOnlyCollection<EndCertificateAndUse> certificatesAndUses)
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

        private async Task SaveCertificatesToStoreAsync(Context context)
        {
            var thumbprintToCertificate = new Dictionary<string, HashedCertificate>();

            CollectCertificates(thumbprintToCertificate, context.Author?.Certificates);
            CollectCertificates(thumbprintToCertificate, context.Repository?.Certificates);

            foreach (var certificate in thumbprintToCertificate.Values)
            {
                if (await _certificateStore.ExistsAsync(certificate.Thumbprint, context.CancellationToken))
                {
                    continue;
                }

                await _certificateStore.SaveAsync(certificate.Certificate, context.CancellationToken);
            }
        }

        private static void CollectCertificates(
            List<EndCertificateAndUse> endCertificatesAndUses,
            List<HashedCertificate> parentCertificates,
            ExtractedCertificates extractedCertificates)
        {
            if (extractedCertificates == null)
            {
                return;
            }

            endCertificatesAndUses.Add(new EndCertificateAndUse(extractedCertificates.SignatureEndCertificate, EndCertificateUse.CodeSigning));
            endCertificatesAndUses.Add(new EndCertificateAndUse(extractedCertificates.TimestampEndCertificate, EndCertificateUse.Timestamping));

            parentCertificates.AddRange(extractedCertificates.SignatureParentCertificates);
            parentCertificates.AddRange(extractedCertificates.TimestampParentCertificates);
        }

        private static void CollectCertificates(
            Dictionary<string, HashedCertificate> thumbprintToCertificate,
            ExtractedCertificates extractedCertificates)
        {
            if (extractedCertificates == null)
            {
                return;
            }

            CollectCertificate(thumbprintToCertificate, extractedCertificates.SignatureEndCertificate);
            CollectCertificates(thumbprintToCertificate, extractedCertificates.SignatureParentCertificates);
            CollectCertificate(thumbprintToCertificate, extractedCertificates.TimestampEndCertificate);
            CollectCertificates(thumbprintToCertificate, extractedCertificates.TimestampParentCertificates);
        }

        private static void CollectCertificates(
            Dictionary<string, HashedCertificate> thumbprintToCertificate,
            IEnumerable<HashedCertificate> hashedCertificates)
        {
            foreach (var hashedCertificate in hashedCertificates)
            {
                CollectCertificate(thumbprintToCertificate, hashedCertificate);
            }
        }

        private static void CollectCertificate(
            Dictionary<string, HashedCertificate> thumbprintToCertificate,
            HashedCertificate hashedCertificate)
        {
            if (!thumbprintToCertificate.ContainsKey(hashedCertificate.Thumbprint))
            {
                thumbprintToCertificate.Add(hashedCertificate.Thumbprint, hashedCertificate);
            }
        }

        private class EndCertificateAndUse
        {
            public EndCertificateAndUse(HashedCertificate hashedCertificate, EndCertificateUse endCertificateUse)
            {
                Certificate = hashedCertificate ?? throw new ArgumentNullException(nameof(hashedCertificate));
                Use = endCertificateUse;
            }

            public HashedCertificate Certificate { get; }
            public EndCertificateUse Use { get; }
        }

        private class Context : IDisposable
        {
            public Context(int packageKey, PrimarySignature primarySignature, CancellationToken cancellationToken)
            {
                PackageKey = packageKey;
                PrimarySignature = primarySignature ?? throw new ArgumentNullException(nameof(primarySignature));
                CancellationToken = cancellationToken;
                Disposables = new List<IDisposable>();
            }

            public int PackageKey { get; }
            public PrimarySignature PrimarySignature { get; }
            public CancellationToken CancellationToken { get; }

            public List<IDisposable> Disposables { get; }

            public SignatureAndCertificates Author { get; set; }
            public SignatureAndCertificates Repository { get; set; }

            public void Dispose()
            {
                foreach (var disposable in Disposables)
                {
                    disposable?.Dispose();
                }
            }
        }

        private class SignatureAndCertificates
        {
            public SignatureAndCertificates(Signature signature, ExtractedCertificates certificates)
            {
                Signature = signature ?? throw new ArgumentNullException(nameof(signature));
                Certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
            }

            public Signature Signature { get; }
            public ExtractedCertificates Certificates { get; }
        }
    }
}
