// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public sealed class CertificateService : CoreCertificateService, ICertificateService
    {
        /// <summary>
        /// This OID is used to mark all Trusted Signing Public Trust certificates.
        /// Source: https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-cert-management
        /// </summary>
        private const string AzureTrustedSigningPublicTrustEku = "1.3.6.1.4.1.311.97.1.0";

        /// <summary>
        /// This OID prefix is specific to the user.
        /// Source: https://learn.microsoft.com/en-us/azure/trusted-signing/concept-trusted-signing-cert-management
        /// </summary>
        private const string AzureTrustedSigningPublicTrustIdentifierPrefix = "1.3.6.1.4.1.311.97.";

        private readonly ICertificateValidator _certificateValidator;
        private readonly IEntityRepository<UserCertificatePattern> _patternRepository;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public CertificateService(
            ICertificateValidator certificateValidator,
            IEntityRepository<Certificate> certificateRepository,
            IEntityRepository<UserCertificatePattern> patternRepository,
            IEntityRepository<User> userRepository,
            IEntitiesContext entitiesContext,
            ICoreFileStorageService fileStorageService,
            IAuditingService auditingService,
            ITelemetryService telemetryService) : base(certificateRepository, fileStorageService)
        {
            _certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
            _patternRepository = patternRepository ?? throw new ArgumentNullException(nameof(patternRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<Certificate> AddCertificateAsync(HttpPostedFileBase file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            _certificateValidator.Validate(file);

            var certificate = await AddCertificateAsync(file.InputStream);

            await _auditingService.SaveAuditRecordAsync(
                new CertificateAuditRecord(AuditedCertificateAction.Add, certificate.Thumbprint));

            _telemetryService.TrackCertificateAdded(certificate.Thumbprint);

            return certificate;
        }

        public async Task ActivateCertificateAsync(string thumbprint, User account)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var certificate = GetCertificate(thumbprint);

            if (certificate == null)
            {
                throw new ArgumentException(Strings.CertificateDoesNotExist, nameof(thumbprint));
            }

            var userCertificate = certificate.UserCertificates.SingleOrDefault(uc => uc.UserKey == account.Key);

            if (userCertificate == null)
            {
                userCertificate = new UserCertificate()
                {
                    CertificateKey = certificate.Key,
                    UserKey = account.Key
                };

                _entitiesContext.UserCertificates.Add(userCertificate);

                await _entitiesContext.SaveChangesAsync();

                await _auditingService.SaveAuditRecordAsync(
                    new CertificateAuditRecord(AuditedCertificateAction.Activate, certificate.Thumbprint));

                _telemetryService.TrackCertificateActivated(thumbprint);
            }
        }

        public async Task<UserCertificatePattern> AddCertificatePatternAsync(CertificatePatternType patternType, string identifier, User account)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(identifier));
            }

            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            switch (patternType)
            {
                case CertificatePatternType.AzureTrustedSigning:
                    identifier = identifier.Trim();
                    if (!identifier.StartsWith(AzureTrustedSigningPublicTrustIdentifierPrefix))
                    {
                        throw new UserSafeException(string.Format(Strings.AzureCodeSigningIdentifierIsNotValid, AzureTrustedSigningPublicTrustIdentifierPrefix));
                    }
                    if (identifier == AzureTrustedSigningPublicTrustEku)
                    {
                        throw new UserSafeException(string.Format(Strings.AzureCodeSigningIdentifierMustNotBePublicTrustEku, AzureTrustedSigningPublicTrustEku));
                    }
                    break;
                default:
                    throw new UserSafeException(Strings.CertificatePatternTypeIsUnrecognized);
            }

            var pattern = GetCertificatePatterns(account)
                .Where(usp => usp.PatternType == patternType && usp.Identifier == identifier)
                .SingleOrDefault();

            if (pattern == null)
            {
                pattern = new UserCertificatePattern
                {
                    User = account,
                    PatternType = patternType,
                    Identifier = identifier,
                };

                _patternRepository.InsertOnCommit(pattern);

                await _entitiesContext.SaveChangesAsync();

                await _auditingService.SaveAuditRecordAsync(
                    new CertificatePatternAuditRecord(AuditedCertificatePatternAction.Add, patternType, identifier));

                _telemetryService.TrackCertificatePatternAdded(patternType, identifier);
            }

            return pattern;
        }

        public async Task DeactivateCertificateAsync(string thumbprint, User account)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(thumbprint));
            }

            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var certificate = GetCertificate(thumbprint);

            if (certificate == null)
            {
                throw new ArgumentException(Strings.CertificateDoesNotExist, nameof(thumbprint));
            }

            var userCertificate = certificate.UserCertificates.SingleOrDefault(uc => uc.UserKey == account.Key);

            if (userCertificate != null)
            {
                _entitiesContext.DeleteOnCommit(userCertificate);

                await _entitiesContext.SaveChangesAsync();

                await _auditingService.SaveAuditRecordAsync(
                    new CertificateAuditRecord(AuditedCertificateAction.Deactivate, certificate.Thumbprint));

                _telemetryService.TrackCertificateDeactivated(thumbprint);
            }
        }

        public async Task DeleteCertificatePatternAsync(int patternKey, User account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            var pattern = GetCertificatePatterns(account).SingleOrDefault(p => p.Key == patternKey);

            if (pattern != null)
            {
                _patternRepository.DeleteOnCommit(pattern);

                await _entitiesContext.SaveChangesAsync();

                await _auditingService.SaveAuditRecordAsync(
                    new CertificatePatternAuditRecord(AuditedCertificatePatternAction.Delete, pattern.PatternType, pattern.Identifier));

                _telemetryService.TrackCertificatePatternDeleted(pattern.PatternType, pattern.Identifier);
            }
        }

        public IEnumerable<Certificate> GetCertificates(User account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return _userRepository.GetAll()
                .Where(u => u.Key == account.Key)
                .SelectMany(u => u.UserCertificates)
                .Select(uc => uc.Certificate);
        }

        public IEnumerable<UserCertificatePattern> GetCertificatePatterns(User account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return _patternRepository
                .GetAll()
                .Where(u => u.Key == account.Key);
        }
    }
}