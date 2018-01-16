// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public class SignaturePartsExtractor : ISignaturePartsExtractor
    {
        private readonly ICertificateStore _certificateStore;
        private readonly IValidationEntitiesContext _entitiesContext;

        public SignaturePartsExtractor(
            ICertificateStore certificateStore,
            IValidationEntitiesContext entitiesContext)
        {
            _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public async Task ExtractAsync(ISignedPackageReader signedPackageReader, CancellationToken token)
        {
            if (!await signedPackageReader.IsSignedAsync(token))
            {
                throw new ArgumentException("The provided package reader must refer to a signed package.", nameof(signedPackageReader));
            }

            // Read the package signature.
            var signature = await signedPackageReader.GetSignatureAsync(token);

            // Extract the certificates found in the package signatures.
            var extractedCertificates = ExtractCertificates(signature);

            // Save the certificates to blob storage.
            await SaveCertificatesToStoreAsync(extractedCertificates, token);

            // Save the certificates to the database.
            await SaveCertificatesToDatabaseAsync(extractedCertificates);
        }

        private ExtractedCertificates ExtractCertificates(Signature signature)
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
                .GetPrimarySignatureTimestampSignatureCertificates(signature);
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

        private async Task SaveCertificatesToDatabaseAsync(ExtractedCertificates extractedCertificates)
        {
            // Initialize the end and parent certificates.
            var thumbprintToEndCertificate = await InitializeEndCertificatesAsync(
                new[]
                {
                    extractedCertificates.SignatureEndCertificate,
                    extractedCertificates.TimestampEndCertificate
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

            // Commit
            await _entitiesContext.SaveChangesAsync();
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
            IEnumerable<HashedCertificate> certificates)
        {
            var thumbprints = certificates
                .Select(x => x.Thumbprint)
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

            foreach (var certificate in certificates)
            {
                if (!thumbprintToEntity.TryGetValue(certificate.Thumbprint, out var entity))
                {
                    entity = new EndCertificate
                    {
                        Status = EndCertificateStatus.Unknown,
                        Thumbprint = certificate.Thumbprint,
                        CertificateChainLinks = new List<CertificateChainLink>(),
                    };
                    _entitiesContext.EndCertificates.Add(entity);

                    thumbprintToEntity[certificate.Thumbprint] = entity;
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
    }
}
