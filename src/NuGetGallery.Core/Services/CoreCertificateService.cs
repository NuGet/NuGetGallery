// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class CoreCertificateService : ICoreCertificateService
    {
        private readonly IEntityRepository<Certificate> _certificateRepository;
        private readonly ICoreFileStorageService _fileStorageService;

        public CoreCertificateService(
            IEntityRepository<Certificate> certificateRepository,
            ICoreFileStorageService fileStorageService)
        {
            _certificateRepository = certificateRepository ?? throw new ArgumentNullException(nameof(certificateRepository));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        }

        public async Task<Certificate> AddCertificateAsync(Stream certificateStream)
        {
            if (certificateStream == null)
            {
                throw new ArgumentNullException(nameof(certificateStream));
            }

            using (var certificateFile = CertificateFile.Create(certificateStream))
            {
                var certificate = GetCertificate(certificateFile.Sha256Thumbprint);

                if (certificate == null)
                {
                    await SaveToFileStorageAsync(certificateFile);

                    certificate = new Certificate()
                    {
#pragma warning disable CS0618 // Only set the SHA1 thumbprint, for backwards compatibility. Never read it.
                        // CodeQL [SM02196] Only set the SHA1 thumbprint, for backwards compatibility. Never read it.
                        Sha1Thumbprint = certificateFile.Sha1Thumbprint,
#pragma warning restore CS0618
                        Thumbprint = certificateFile.Sha256Thumbprint,
                        UserCertificates = new List<UserCertificate>()
                    };

                    _certificateRepository.InsertOnCommit(certificate);

                    await _certificateRepository.CommitChangesAsync();
                }

                return certificate;
            }
        }

        protected async Task SaveToFileStorageAsync(CertificateFile certificateFile)
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

        protected Certificate GetCertificate(string thumbprint)
        {
            return _certificateRepository.GetAll()
                .Where(c => c.Thumbprint == thumbprint)
                .Include(c => c.UserCertificates)
                .SingleOrDefault();
        }
    }
}