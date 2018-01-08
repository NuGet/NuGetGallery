// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Storage;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class CertificateStore : ICertificateStore
    {
        private const string _containerSubDirectory = "sha256";
        private const string _fileExtension = ".cer";

        private readonly ILogger<CertificateStore> _logger;
        private readonly IStorage _storage;

        public CertificateStore(
            IStorage storage,
            ILogger<CertificateStore> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> ExistsAsync(string sha256Thumbprint, CancellationToken cancellationToken)
        {
            if (sha256Thumbprint == null)
            {
                throw new ArgumentNullException(nameof(sha256Thumbprint));
            }

            return _storage.ExistsAsync(GetBlobFileName(sha256Thumbprint), cancellationToken);
        }

        public async Task<X509Certificate2> LoadAsync(string sha256Thumbprint, CancellationToken cancellationToken)
        {
            if (sha256Thumbprint == null)
            {
                throw new ArgumentNullException(nameof(sha256Thumbprint));
            }

            var uri = _storage.ResolveUri(GetBlobFileName(sha256Thumbprint));

            _logger.LogInformation("Loading certificate with SHA-256 thumbprint {Thumbprint} from URI {BlobUri}", sha256Thumbprint, uri);

            var storageContent = await _storage.Load(uri, CancellationToken.None);
            if (storageContent == null)
            {
                _logger.LogError(
                        Error.LoadCertificateFromStorageFailed,
                        "The certificate with SHA-256 thumbprint {Thumbprint} could not be loaded from storage URI {BlobUri}.",
                        sha256Thumbprint,
                        uri);

                throw new InvalidOperationException($"Failed to load certificate with SHA-256 thumbprint {sha256Thumbprint} from URI {uri}");
            }

            byte[] rawData;
            using (var stream = storageContent.GetContentStream())
            {
                using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer);
                    rawData = buffer.ToArray();
                }
            }

            var certificate = new X509Certificate2(rawData);
            var certificateSha256ComputedThumbprint = certificate.ComputeSHA256Thumbprint();

            // Verify the certificate's thumbprint each time the certificate is downloaded from blob storage
            if (!string.Equals(certificateSha256ComputedThumbprint, sha256Thumbprint, StringComparison.Ordinal))
            {
                _logger.LogError(
                        Error.LoadedCertificateThumbprintDoesNotMatch,
                        "The loaded certificate did not match the expected SHA-256 thumbprint {ExpectedThumbprint} (actual SHA-256 thumbprint: {ActualThumbprint}).",
                        sha256Thumbprint,
                        certificateSha256ComputedThumbprint);

                throw new InvalidOperationException($"The loaded certificate did not match the expected SHA-256 thumbprint {sha256Thumbprint} (actual SHA-256 thumbprint: {certificateSha256ComputedThumbprint}).");
            }

            _logger.LogInformation("Loaded certificate with SHA-256 thumbprint {Thumbprint} from URI {blobUri}", sha256Thumbprint, uri);

            return certificate;
        }

        public Task SaveAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            var sha256Thumbprint = certificate.ComputeSHA256Thumbprint();
            var uri = _storage.ResolveUri(GetBlobFileName(sha256Thumbprint));

            _logger.LogInformation("Saving certificate with SHA-256 thumbprint {Thumbprint} to URI {BlobUri}", sha256Thumbprint, uri);

            return _storage.Save(
                uri,
                new StreamStorageContent(new MemoryStream(certificate.RawData)),
                overwrite: false,
                cancellationToken: cancellationToken);
        }

        private static string GetBlobFileName(string sha256Thumbprint)
        {
            return $"{_containerSubDirectory}/{sha256Thumbprint}{_fileExtension}".ToLowerInvariant();
        }
    }
}