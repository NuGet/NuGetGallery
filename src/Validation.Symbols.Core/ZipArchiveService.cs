// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class ZipArchiveService : IZipArchiveService
    {
        ILogger<ZipArchiveService> _logger;

        public ZipArchiveService(ILogger<ZipArchiveService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
        }

        /// <summary>
        /// Returns the files from a zip stream. The results are filtered for the files with the specified exceptions.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="matchingExtensions"></param>
        /// <returns></returns>
        public List<string> ReadFilesFromZipStream(Stream stream, params string[] matchingExtensions)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            List<string> entries;
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                entries = ReadFilesFromZipStream(archive.Entries, matchingExtensions).ToList();
            }
            // Set the position back to 0 in case that the stream advances
            stream.Position = 0;
            return entries;
        }

        public List<string> ExtractFilesFromZipStream(Stream stream, string targetDirectory, IEnumerable<string> filterFileExtension = null, IEnumerable<string> filterFileNames = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            List<string> entries;
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                entries = Extract(archive.Entries, targetDirectory, filterFileExtension, filterFileNames).ToList();
            }
            // Set the position back to 0 in case that the stream advances
            stream.Position = 0;
            return entries;
        }

        public IEnumerable<string> Extract(IReadOnlyCollection<ZipArchiveEntry> entries,
          string targetDirectory,
          IEnumerable<string> filterFileExtensions = null,
          IEnumerable<string> symbolFilter = null)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            if (targetDirectory == null)
            {
                throw new ArgumentNullException(nameof(targetDirectory));
            }

            var symbolFilterWithoutExtensions = RemoveExtension(symbolFilter);

            return entries.
                   Where(e => !string.IsNullOrEmpty(e.Name)).
                   Where((e) =>
                   {
                       if(filterFileExtensions != null && !filterFileExtensions.Contains(Path.GetExtension(e.FullName)))
                       {
                           return false;
                       }
                       if (symbolFilterWithoutExtensions == null)
                       {
                           return true;
                       }
                       return symbolFilterWithoutExtensions.Contains(RemoveExtension(e.FullName), StringComparer.OrdinalIgnoreCase);
                   }).
                   Select((e) =>
                   {
                       OnExtract(e, targetDirectory);
                       return e.FullName;
                   });
        }

        /// <summary>
        /// Overwrite to not extract the files to the <paramref name="targetDirectory"/>.
        /// </summary>
        /// <param name="entry"><see cref="ZipArchiveEntry" /> entry.</param>
        /// <param name="targetDirectory">The target directory to extract the compressed data.</param>
        public virtual void OnExtract(ZipArchiveEntry entry, string targetDirectory)
        {
            string destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            entry.ExtractToFile(destinationPath);
        }

        /// <summary>
        /// Removes all the file extensions.
        /// </summary>
        /// <param name="source"></param>
        /// <returns>The list of files with extensions removed.</returns>
        public static IEnumerable<string> RemoveExtension(IEnumerable<string> files)
        {
            if(files == null)
            {
                return null;
            }
            return files.Select(RemoveExtension);
        }

        /// <summary>
        /// Removes the extension for a file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string RemoveExtension(string file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }
            var directory = Path.GetDirectoryName(file);
            return string.IsNullOrEmpty(directory) ? Path.GetFileNameWithoutExtension(file) : string.Concat(directory, "\\", Path.GetFileNameWithoutExtension(file));
        }

        /// <summary>
        /// Reads all the entries from a zip streams and filter them based on the set of <paramref name="matchingExtensions"/>.
        /// </summary>
        /// <param name="entries">The <see cref="ZipArchiveEntry"/> collection.</param>
        /// <param name="matchingExtensions">The extensions used for filter.</param>
        /// <returns></returns>
        public static IEnumerable<string> ReadFilesFromZipStream(IReadOnlyCollection<ZipArchiveEntry> entries, params string[] matchingExtensions)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
            if (matchingExtensions == null)
            {
                throw new ArgumentNullException(nameof(matchingExtensions));
            }
            return entries.
                Where(e => !string.IsNullOrEmpty(e.Name)).
                Where(e => matchingExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase)).
                Select(e => e.FullName);
        }

        public async Task<bool> ValidateZipAsync(Stream stream, string streamName, CancellationToken token)
        {
            // See: https://github.com/NuGet/NuGet.Client/blob/f168e1667d548e3138b3f1e93c34d557b0deeda3/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs#L234
            using (var packageArchiveReader = new PackageArchiveReader(stream, leaveStreamOpen: true))
            {
                try
                {
                    await packageArchiveReader.ValidatePackageEntriesAsync(token);
                    return true;         
                }
                catch (UnsafePackageEntryException unsafePackageEntryException)
                {
                    _logger.LogError(Error.PackageHasUnsecureEntries, unsafePackageEntryException, "Archive with unsafe entries. {StreamName}", streamName);
                    return false;
                }
                catch (PackagingException packagingException)
                {
                    _logger.LogError(Error.PackagingException, packagingException, "The package is not in the correct format. {StreamName}", streamName);
                    return false;
                }
            }
        }
    }
}
