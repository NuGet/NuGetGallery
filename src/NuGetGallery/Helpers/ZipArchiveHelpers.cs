// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGetGallery
{
    public static class ZipArchiveHelpers
    {
        /// <summary>
        /// This method checks all the <see cref="ZipArchiveEntry"/> in a given 
        /// <see cref="Stream"/> if it has an entry with a future datetime or a double slash in the path, 
        /// it will return the first entry found in the future or with a double slash in the path.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> object to verify</param>
        /// <param name="entry"><see cref="ZipArchiveEntry"/> found with future entry.</param>
        /// <returns>True if <see cref="Stream"/> contains an entry in future, false otherwise.</returns>
        public static InvalidZipEntry ValidateArchiveEntries(Stream stream, out ZipArchiveEntry entry)
        {
            entry = null;

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var reference = DateTime.UtcNow.AddDays(1); // allow "some" clock skew

                var entryInTheFuture = archive.Entries.FirstOrDefault(
                    e => e.LastWriteTime.UtcDateTime > reference);

                if (entryInTheFuture != null)
                {
                    entry = entryInTheFuture;
                    return InvalidZipEntry.InFuture;
                }

                var entryWithDoubleForwardSlash = archive.Entries.FirstOrDefault(
                    e => e.FullName.Contains("//"));

                if (entryWithDoubleForwardSlash != null)
                {
                    entry = entryWithDoubleForwardSlash;
                    return InvalidZipEntry.DoubleForwardSlashesInPath;
                }

                var entryWithDoubleBackSlash = archive.Entries.FirstOrDefault(
                    e => e.FullName.Contains("\\\\"));

                if (entryWithDoubleBackSlash != null)
                {
                    entry = entryWithDoubleBackSlash;
                    return InvalidZipEntry.DoubleBackwardSlashesInPath;
                }
            }

            return InvalidZipEntry.None;
        }
    }
}