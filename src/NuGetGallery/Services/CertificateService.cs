// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public sealed class CertificateService : ICertificateService
    {
        private readonly ICertificateValidator _certificateValidator;
        private readonly IEntityRepository<Certificate> _certificateRepository;
        private readonly IEntityRepository<User> _userRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IFileStorageService _fileStorageService;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public CertificateService(
            ICertificateValidator certificateValidator,
            IEntityRepository<Certificate> certificateRepository,
            IEntityRepository<User> userRepository,
            IEntitiesContext entitiesContext,
            IFileStorageService fileStorageService,
            IAuditingService auditingService,
            ITelemetryService telemetryService)
        {
            _certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
            _certificateRepository = certificateRepository ?? throw new ArgumentNullException(nameof(certificateRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
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

            using (var certificateFile = CertificateFile.Create(file.InputStream))
            {
                var certificate = GetCertificate(certificateFile.Sha256Thumbprint);

                if (certificate == null)
                {
                    await SaveToFileStorageAsync(certificateFile);

                    certificate = new Certificate()
                    {
#pragma warning disable CS0618 // Only set the SHA1 thumbprint, for backwards compatibility. Never read it.
                        Sha1Thumbprint = certificateFile.Sha1Thumbprint,
#pragma warning restore CS0618
                        Thumbprint = certificateFile.Sha256Thumbprint,
                        UserCertificates = new List<UserCertificate>()
                    };

                    _certificateRepository.InsertOnCommit(certificate);

                    await _certificateRepository.CommitChangesAsync();

                    await _auditingService.SaveAuditRecordAsync(
                        new CertificateAuditRecord(AuditedCertificateAction.Add, certificate.Thumbprint));

                    _telemetryService.TrackCertificateAdded(certificateFile.Sha256Thumbprint);
                }

                return certificate;
            }
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

        private async Task SaveToFileStorageAsync(CertificateFile certificateFile)
        {
            var filePath = $"SHA-256/{certificateFile.Sha256Thumbprint}{CoreConstants.CertificateFileExtension}";

            try
            {
                await _fileStorageService.SaveFileAsync(
                    CoreConstants.Folders.UserCertificatesFolderName,
                    filePath,
                    certificateFile.Stream,
                    overwrite: false);
            }
            catch (FileAlreadyExistsException)
            {
                // A certificate is being uploaded again.
                // The fact that the certificate already exists in storage is ignorable.
            }
        }

        private Certificate GetCertificate(string thumbprint)
        {
            return _certificateRepository.GetAll()
                .Where(c => c.Thumbprint == thumbprint)
                .Include(c => c.UserCertificates)
                .SingleOrDefault();
        }
    }
}