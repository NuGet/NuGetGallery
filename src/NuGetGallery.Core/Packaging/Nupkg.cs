// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using NuGet;

namespace NuGetGallery.Packaging
{
    // Note - a lot of this code is based on the OPC format which we've traditionally used to generate (save) nupkg files.
    // This class is intended for *reading* the packages only.
    // We deviate from a typical OPC package reader in a couple notable ways:
    // 1) We never look at [Content_Types] and .rels, and don't suppor their semantics.
    // 2) We don't actually support reading files that are stored as interleaved parts (/[0].piece etc), 
    //    although we do recognize the part exists (with its proper intended part name).
    public sealed class Nupkg : INupkg
    {
        private const int MaxManifestSize = 1024*1024;
        private static readonly Regex PieceSpecifierRegex = new Regex(@"\[(0|[1-9][1-9]*)\]\.piece", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly Stream _stream;
        private readonly ZipArchive _archive;
        private readonly Manifest _manifest;

        private HashSet<string> _parts;

        public IPackageMetadata Metadata
        {
            get { return _manifest.Metadata; }
        }

        public IEnumerable<string> Parts
        { 
            get { return _parts ?? (_parts = SafelyLoadParts(_archive)); }
        }

        /// <summary>
        /// Safely load Nupkg manifest data and file contents list from an untrusted zip file stream.
        /// May modify the .Position of the stream.
        /// </summary>
        public Nupkg(Stream stream, bool leaveOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (!stream.CanSeek)
            {
                var originalStream = stream;
                stream = new MemoryStream(originalStream.ReadAllBytes(), writable: false);
                if (!leaveOpen)
                {
                    originalStream.Dispose();
                }
            }

            _stream = stream;
            _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);         
            _manifest = SafelyLoadManifest(_archive);
            _parts = SafelyLoadParts(_archive);
        }

        public void Dispose()
        {
            _archive.Dispose();
        }

        /// <summary>
        /// Gets a list of all the files in the package.
        /// Filter out parts which are obviously OPC metadata.
        /// </summary>
        public IEnumerable<string> GetFiles()
        {
            foreach (string part in Parts)
            {
                if (part.EndsWith("/.rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (part.EndsWith("/[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (part.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Debug.Assert(part[0] == '/');
                yield return part.Substring(1).Replace('/', Path.DirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Load the nuspec manifest data only from an untrusted zip file stream.
        /// May modify the .Position of the stream.
        /// </summary>
        public static Manifest SafelyLoadManifest(Stream stream, bool leaveOpen)
        {
            using (var za = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen))
            {
                return SafelyLoadManifest(za);
            }
        }

        private static Manifest SafelyLoadManifest(ZipArchive archive)
        {
            var manifestEntry = archive.Entries.SingleOrDefault(entry =>
                    entry.FullName.IndexOf("/", StringComparison.Ordinal) == -1
                    && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                );

            if (manifestEntry == null)
            {
                throw new InvalidPackageException("A manifest was not found at the root of the package.");
            }

            using (var safeStream = GetSizeVerifiedFileStream(manifestEntry, MaxManifestSize))
            {
                return Manifest.ReadFrom(
                    safeStream,
                    NullPropertyProvider.Instance,
                    validateSchema: true); // Validating schema hopefully helps ensure quality of packages on the gallery
            }
        }

        /// <summary>
        /// Load the list of OpenPackage parts (files) from an untrusted zip file stream, as filenames.
        /// Excludes metadata parts such as [Content_Types], .rels files, and TODO .pmd files or whatever they were.
        /// </summary>
        private static HashSet<string> SafelyLoadParts(ZipArchive archive)
        {
            var ret = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                bool interleaved;
                string partName = GetLogicalPartName(entry.FullName, out interleaved).ToString();
                if (IsValidPartName(partName))
                {
                    bool added = ret.Add(partName);
                    if (!added && !interleaved)
                    {
                        throw new InvalidDataException(string.Format("Duplicate Part Name {0} found in the package.",partName));
                    }
                }

            }

            return ret;
        }

        internal static bool IsValidPartName(string logicalPartName)
        {
            Debug.Assert(logicalPartName != null);
            Debug.Assert(logicalPartName.Length >= 1);
            Debug.Assert(logicalPartName.StartsWith("/", StringComparison.Ordinal));

            var segments = logicalPartName.Split('/');

            // note: Skip(1): since logicalPartName starts with '/', there is always an empty string at the start
            foreach (string segment in segments.Skip(1))
            {
                // Part names can't be empty (no double slash //)
                // Part names can't end in .
                // Part name segments must contain a non-dot character.
                if (segment.Length == 0 ||
                    segment.All(c => c == '.') ||
                    segment[segment.Length - 1] == '.')
                {
                    return false;
                }
            }

            return true;
        }

        internal static Uri GetLogicalPartName(string zipEntryName, out bool interleaved)
        {
            // OPC 10.2.4 Mapping ZIP Item Names to Part Names
            // To map ZIP item names to part names, the package implementer shall perform, in order, the following steps
            // [M3.6]: 1. Map the ZIP item names to logical item names by adding a forward slash (/) to each of the ZIP item names. 
            // 2. Map the obtained logical item names to part names. For more information, see §10.1.3.4.

            interleaved = false;
            string logicalPartName = zipEntryName;

            // Note: ZIP Entries path format (APPNOTE.TXT 6.3.3)
            // "4.4.17.1 The name of the file, with optional relative path.
            //  The path stored MUST not contain a drive or
            //  device letter, or a leading slash.  All slashes
            //  MUST be forward slashes '/' as opposed to
            //  backwards slashes '\' for compatibility with Amiga
            //  and UNIX file systems etc..."
            if (zipEntryName.Contains('\\'))
            {
                throw new InvalidDataException(string.Format("The zip entry {0} has backward slash as path separator and will not be compatible in non-Windows OS",zipEntryName));
            }

            // If it matches the pattern for 'piece of interleaved part' then interleaved is true, and we trim off the piece specifier
            // e.g. "spine.xml/[0].piece"
            int lastSlash = zipEntryName.LastIndexOf('/');
            if (lastSlash > 0)
            {
                if (PieceSpecifierRegex.Match(zipEntryName, lastSlash + 1).Success)
                {
                    interleaved = true;
                    logicalPartName = zipEntryName.Substring(0, lastSlash);
                }
            }

            // Finally logical part names should start with a '/'. OPC Zip Embedding Convention says to prepend the slash.
            Uri ret = new Uri('/' + logicalPartName, UriKind.Relative);
            Debug.Assert(!ret.IsAbsoluteUri);
            return ret;
        }

        public IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            var fileFrameworks = new HashSet<FrameworkName>();
            fileFrameworks.AddRange(Metadata.FrameworkAssemblies
                .SelectMany(f => f.SupportedFrameworks)
                .Where(sf => sf != null));

            foreach (var file in GetFiles())
            {
                string effectivePath;

                //  depending on the version of the client the nupkg may contain either a full url encoded filename or not
                //  url decoding will replace '+' with ' ' which will be a problem if the filename happened not to be url encoded
                //  this impacts portable lib paths all of which contain '+'
                //  solution is to always url decode but then put back in plus for a space - then the data should match the query

                string decodedFilename = WebUtility.UrlDecode(file);
                decodedFilename = decodedFilename.Replace(' ', '+');

                var frameworkName = VersionUtility.ParseFrameworkNameFromFilePath(decodedFilename, out effectivePath);
                if (frameworkName != null)
                {
                    fileFrameworks.Add(frameworkName);
                }
            }

            return fileFrameworks;
        }

        public Stream GetStream()
        {
            _stream.Position = 0;
            return _stream;
        }

        public Stream GetSizeVerifiedFileStream(string filePath, int maxSize)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException("filePath");
            }

            var zipEntry = _archive.GetEntry(CanonicalName(filePath));
            if (zipEntry == null)
            {
                throw new ArgumentException("Zip entry does not exist.");
            }

            return GetSizeVerifiedFileStream(zipEntry, maxSize);
        }

        // Needs to be able to convert: \tools\NuGet.exe to something close enough to tools/NuGet.exe
        internal static string CanonicalName(string fileName)
        {
            Debug.Assert(fileName != null);

            fileName = fileName.Replace('\\', '/');

            if (fileName.Length > 1 && fileName[0] == '/')
            {
                return fileName.Substring(1);
            }

            return fileName;
        }

        private static Stream GetSizeVerifiedFileStream(ZipArchiveEntry zipEntry, int maxSize)
        {
            Debug.Assert(zipEntry != null);

            // claimedLength = What the zip file header claims is the uncompressed length
            // let's not be too trusting when it comes to that claim.
            var claimedLength = zipEntry.Length;
            if (claimedLength < 0)
            {
                throw new InvalidDataException("The zip entry size is invalid.");
            }

            if (claimedLength > maxSize)
            {
                throw new InvalidDataException("The zip entry is larger than the allowed size.");
            }

            // Read at most the claimed number of bytes from the array.
            byte[] safeBytes;
            int bytesRead;
            using (Stream unsafeStream = zipEntry.Open())
            {
                safeBytes = new byte[claimedLength];
                bytesRead = unsafeStream.Read(safeBytes, 0, (int)claimedLength);
                if (bytesRead != claimedLength)
                {
                    throw new InvalidDataException("The zip entry's claimed decompressed size is incorrect.");
                }
            }

            return new MemoryStream(safeBytes, 0, bytesRead);
        }
    }
}