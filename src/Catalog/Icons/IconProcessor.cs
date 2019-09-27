// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class IconProcessor : IIconProcessor
    {
        private const string DefaultCacheControl = "max-age=120";
        private const int MaxExternalIconSize = 1024 * 1024; // 1 MB

        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<IconProcessor> _logger;

        public IconProcessor(
            ITelemetryService telemetryService,
            ILogger<IconProcessor> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Uri> CopyIconFromExternalSource(
            Stream iconDataStream,
            IStorage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion)
        {
            var destinationUri = destinationStorage.ResolveUri(destinationStoragePath);

            var iconData = await GetStreamBytesAsync(iconDataStream, MaxExternalIconSize, cancellationToken);
            if (iconData == null)
            {
                return null;
            }

            var contentType = DetermineContentType(iconData, onlyGallerySupported: false);
            if (string.IsNullOrWhiteSpace(contentType))
            {
                _logger.LogInformation("Failed to determine image type.");
                return null;
            }
            _logger.LogInformation("Content type for {PackageId} {PackageVersion} {ContentType}", packageId, normalizedPackageVersion, contentType);
            var content = new ByteArrayStorageContent(iconData, contentType, DefaultCacheControl);
            await destinationStorage.SaveAsync(destinationUri, content, cancellationToken);
            _telemetryService.TrackExternalIconIngestionSuccess(packageId, normalizedPackageVersion);
            return destinationUri;
        }

        public async Task DeleteIcon(
            Storage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion)
        {
            _logger.LogInformation("Deleting icon blob {IconPath}", destinationStoragePath);
            if (destinationStorage.Exists(destinationStoragePath))
            {
                var iconUri = new Uri(destinationStorage.BaseAddress, destinationStoragePath);
                await destinationStorage.DeleteAsync(iconUri, cancellationToken);
            }
        }

        public async Task<Uri> CopyEmbeddedIconFromPackage(
            Stream packageStream,
            string iconFilename,
            IStorage destinationStorage,
            string destinationStoragePath,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion)
        {
            var iconPath = PathUtility.StripLeadingDirectorySeparators(iconFilename);
            var destinationUri = destinationStorage.ResolveUri(destinationStoragePath);

            await ExtractAndStoreIconAsync(packageStream, iconPath, destinationStorage, destinationUri, cancellationToken, packageId, normalizedPackageVersion);
            return destinationUri;
        }

        private async Task ExtractAndStoreIconAsync(
            Stream packageStream,
            string iconPath,
            IStorage destinationStorage,
            Uri destinationUri,
            CancellationToken cancellationToken,
            string packageId,
            string normalizedPackageVersion)
        {
            using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var iconEntry = zipArchive.Entries.FirstOrDefault(e => e.FullName.Equals(iconPath, StringComparison.InvariantCultureIgnoreCase));
                if (iconEntry != null)
                {
                    using (var iconStream = iconEntry.Open())
                    {
                        _logger.LogInformation("Extracting icon to the destination storage {DestinationUri}", destinationUri);
                        var iconData = await GetStreamBytesAsync(iconStream, cancellationToken);
                        // files with embedded icons are expected to only contain image types Gallery allows in
                        // (jpeg and png). Others are still going to be saved for correctness sake, but we won't
                        // try to determine their type (they shouldn't have made this far anyway).
                        var contentType = DetermineContentType(iconData, onlyGallerySupported: true); 
                        _logger.LogInformation("Content type for {PackageId} {PackageVersion} {ContentType}", packageId, normalizedPackageVersion, contentType);
                        var iconContent = new ByteArrayStorageContent(iconData, contentType, DefaultCacheControl);
                        await destinationStorage.SaveAsync(destinationUri, iconContent, cancellationToken);
                        _telemetryService.TrackIconExtractionSuccess(packageId, normalizedPackageVersion);
                        _logger.LogInformation("Done");
                    }
                }
                else
                {
                    _telemetryService.TrackIconExtractionFailure(packageId, normalizedPackageVersion);
                    _logger.LogWarning("Zip archive entry {IconPath} does not exist", iconPath);
                }
            }
        }

        private static async Task<byte[]> GetStreamBytesAsync(Stream sourceStream, CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                await sourceStream.CopyToAsync(ms, 8192, cancellationToken);
                return ms.ToArray();
            }
        }

        private async Task<byte[]> GetStreamBytesAsync(Stream sourceStream, int maxBytes, CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                var totalBytesRead = 0;
                var bytesRead = 0;
                do
                {
                    bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    totalBytesRead += bytesRead;
                    await ms.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                } while (bytesRead > 0 && totalBytesRead < maxBytes + 1);

                if (totalBytesRead > maxBytes)
                {
                    _logger.LogInformation("Source data too long, discarding.");
                    return null;
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// The PNG file header bytes. All PNG files are expected to have those at the beginning of the file.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/TR/PNG/#5PNG-file-signature
        /// </remarks>
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// The JPG file header bytes.
        /// </summary>
        /// <remarks>
        /// Technically, JPEG start with two byte SOI (start of image) segment: FFD8, followed by several other segments or fill bytes.
        /// All of the segments start with FF, and fill bytes are FF, so we check the first 3 bytes instead of the first two.
        /// https://www.w3.org/Graphics/JPEG/itu-t81.pdf "B.1.1.2 Markers"
        /// </remarks>
        private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };

        /// <summary>
        /// The GIF87a file header.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/Graphics/GIF/spec-gif87.txt
        /// </remarks>
        private static readonly byte[] Gif87aHeader = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 };

        /// <summary>
        /// The GIF89a file header.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/Graphics/GIF/spec-gif89a.txt
        /// </remarks>
        private static readonly byte[] Gif89aHeader = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };

        /// <summary>
        /// The .ico file "header".
        /// </summary>
        /// <remarks>
        /// This is the first 4 bytes of the ICONDIR structure expected for .ico files
        /// https://docs.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
        /// </remarks>
        private static readonly byte[] IcoHeader = new byte[] { 0x00, 0x00, 0x01, 0x00 };

        private static string DetermineContentType(byte[] imageData, bool onlyGallerySupported)
        {
            // checks are ordered by format popularity among external icons for existing packages

            if (ArrayStartsWith(imageData, PngHeader))
            {
                return "image/png";
            }

            if (ArrayStartsWith(imageData, JpegHeader))
            {
                return "image/jpeg";
            }

            if (onlyGallerySupported)
            {
                return "";
            }

            if (ArrayStartsWith(imageData, IcoHeader))
            {
                return "image/x-icon";
            }

            if (ArrayStartsWith(imageData, Gif89aHeader) || ArrayStartsWith(imageData, Gif87aHeader))
            {
                return "image/gif";
            }

            if (IsSvgData(imageData))
            {
                return "image/svg+xml";
            }

            return "";
        }

        private static bool IsSvgData(byte[] imageData)
        {
            bool isTextFile = imageData.All(b => b >= 32 || b == '\n' || b == '\f' || b == '\r' || b == '\t');
            if (!isTextFile)
            {
                return false;
            }

            var stringContent = Encoding.UTF8.GetString(imageData);

            return !stringContent.Contains("<html") && stringContent.Contains("<svg");
        }

        private static bool ArrayStartsWith(byte[] array, byte[] expectedBytes)
        {
            if (array.Length < expectedBytes.Length)
            {
                return false;
            }

            for (int index = 0; index < expectedBytes.Length; ++index)
            {
                if (array[index] != expectedBytes[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
