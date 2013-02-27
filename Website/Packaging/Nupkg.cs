using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using NuGet;

namespace NuGetGallery
{
    public class Nupkg : INupkg
    {
        private static readonly Regex PieceSpecifierRegex;

        private readonly Stream _stream;
        private readonly ZipArchive _archive;
        private readonly Manifest _manifest;

        private HashSet<string> _parts;

        public IPackageMetadata Metadata
        {
            get { return _manifest.Metadata; }
        }

        internal IEnumerable<string> Parts
        { 
            get { return _parts ?? (_parts = SafelyLoadParts(_archive)); }
        }

        static Nupkg()
        {
            PieceSpecifierRegex = new Regex(@"\[(0|[1-9][1-9]*)\]\.piece", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

            _stream = stream;
            _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
            _manifest = SafelyLoadManifest(_archive);
        }

        public void Dispose()
        {
            _archive.Dispose();
        }

        public IEnumerable<string> GetFiles()
        {
            // Filter out parts which are obviously OPC metadata
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
                yield return part.Substring(1);
            }
        }

        /// <summary>
        /// Load the nuspec manifest data only from an untrusted zip file stream.
        /// May modify the .Position of the stream.
        /// </summary>
        public static Manifest SafelyLoadManifest(Stream stream)
        {
            using (var za = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                return SafelyLoadManifest(za);
            }
        }

        private static Manifest SafelyLoadManifest(ZipArchive archive)
        {
            var nuspecs = archive.Entries.Where(entry =>
                    (entry.Name.IndexOf("/", StringComparison.Ordinal) == -1)
                    && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                ).ToArray();

            if (nuspecs.Length < 1)
            {
                throw new InvalidOperationException("Package does not contain a .nuspec manifest.");
            }

            if (nuspecs.Length > 1)
            {
                throw new InvalidOperationException("Package contains multiple .nuspec manifests.");
            }

            ZipArchiveEntry manifestEntry = nuspecs[0];
            using (var safeStream = GetCheckedFileStream(manifestEntry, 1048576))
            {
                return Manifest.ReadFrom(
                        safeStream,
                        NullPropertyProvider.Instance,
                        validateSchema: false);
            }
        }

        /// <summary>
        /// Load the list of OpenPackage parts (files) from an untrusted zip file stream, as filenames.
        /// Excludes metadata parts such as [Content_Types], .rels files, and TODO .pmd files or whatever they were.
        /// </summary>
        private static HashSet<string> SafelyLoadParts(ZipArchive archive)
        {
            var ret = new HashSet<string>();
            foreach (var entry in archive.Entries)
            {
                bool interleaved;
                string partName = GetLogicalPartName(entry.FullName, out interleaved).ToString();
                if (!IsInvalidPartName(partName))
                {
                    bool added = ret.Add(partName);
                    if (!added && !interleaved)
                    {
                        throw new InvalidOperationException("Duplicate Part Name");
                    }
                }

            }

            return ret;
        }

        internal static bool IsInvalidPartName(string logicalPartName)
        {
            Debug.Assert(logicalPartName != null);
            Debug.Assert(logicalPartName.Length >= 1);
            Debug.Assert(logicalPartName.StartsWith("/", StringComparison.Ordinal));

            if (logicalPartName.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool segmentHasNoNonDotCharacters = false;
            bool segmentEndsInDot = false;
            foreach (char c in logicalPartName)
            {
                switch (c)
                {
                    case '/':
                        if (segmentEndsInDot || segmentHasNoNonDotCharacters)
                        {
                            return true; // Part name segments must contain a non-dot character.
                        }

                        segmentHasNoNonDotCharacters = true;
                        break;

                    case '.':
                        segmentEndsInDot = true;
                        break;

                    default:
                        segmentHasNoNonDotCharacters = false;
                        segmentEndsInDot = false;
                        break;
                }
            }

            if (segmentEndsInDot || segmentHasNoNonDotCharacters)
            {
                return true; // Part name segments must contain a non-dot character, and may not end in a dot.
            }

            return false;
        }

        internal static Uri GetLogicalPartName(string zipEntryName, out bool interleaved)
        {
            // OPC 10.2.4 Mapping ZIP Item Names to Part Names
            // To map ZIP item names to part names, the package implementer shall perform, in order, the following steps
            // [M3.6]: 1. Map the ZIP item names to logical item names by adding a forward slash (/) to each of the ZIP item names. 
            // 2. Map the obtained logical item names to part names. For more information, see §10.1.3.4.

            interleaved = false;
            string logicalPartName = zipEntryName;

            // Convert '\' to '/' because it will happen anyway during URI-ifying.

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

            // Finally logical part names should start with a '/'. Zip Embedding Convention says to prepend the slash.
            Uri ret = new Uri('/' + logicalPartName, UriKind.Relative);
            Debug.Assert(!ret.IsAbsoluteUri);
            return ret;
        }

        public IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            var fileFrameworks = new HashSet<FrameworkName>();
            fileFrameworks.AddRange(Metadata.FrameworkAssemblies.SelectMany(f => f.SupportedFrameworks));

            foreach (var file in GetFiles())
            {
                string effectivePath;
                fileFrameworks.Add(VersionUtility.ParseFrameworkNameFromFilePath(file, out effectivePath));
            }

            return fileFrameworks;
        }

        public Stream GetStream()
        {
            _stream.Position = 0;
            return _stream;
        }

        public Stream GetCheckedFileStream(string filePath, int maxSize)
        {
            var zipEntry = _archive.GetEntry(filePath);
            return GetCheckedFileStream(zipEntry, maxSize);
        }

        private static Stream GetCheckedFileStream(ZipArchiveEntry manifestEntry, int maxSize)
        {
            // claimedLength = What the zip file header claims is the uncompressed length
            // let's not be too trusting when it comes to that claim.
            var claimedLength = manifestEntry.Length;
            if (claimedLength < 0)
            {
                throw new InvalidOperationException("The zip entry size is invalid.");
            }

            if (claimedLength > maxSize)
            {
                throw new InvalidOperationException("The zip entry is larger than the allowed size.");
            }

            // Read at most the claimed number of bytes from the array.
            byte[] safeBytes;
            int bytesRead;
            using (Stream unsafeStream = manifestEntry.Open())
            {
                safeBytes = new byte[claimedLength];
                bytesRead = unsafeStream.Read(safeBytes, 0, (int)claimedLength);
                if (bytesRead != claimedLength)
                {
                    throw new InvalidDataException("The .nuspec zip entry decompressed size is incorrect.");
                }
            }

            return new MemoryStream(safeBytes, 0, bytesRead);
        }
    }
}