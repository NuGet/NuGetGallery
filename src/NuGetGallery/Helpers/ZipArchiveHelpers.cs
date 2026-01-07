// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

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

                ZipArchiveEntry entryInTheFuture = archive.Entries.FirstOrDefault(
                    e => e.LastWriteTime.UtcDateTime > reference);

                if (entryInTheFuture != null)
                {
                    entry = entryInTheFuture;
                    return InvalidZipEntry.InFuture;
                }

                ZipArchiveEntry entryWithDoubleForwardSlash = archive.Entries.FirstOrDefault(
                    e => e.FullName.Contains("//"));

                if (entryWithDoubleForwardSlash != null)
                {
                    entry = entryWithDoubleForwardSlash;
                    string entryFullName = NormalizeForwardSlashesInPath(entry.FullName);
                    bool duplicateExist = archive.Entries.Select(e => NormalizeForwardSlashesInPath(e.FullName))
                        .Count(f => string.Equals(f, entryFullName, StringComparison.OrdinalIgnoreCase)) > 1;

                    if (duplicateExist)
                        return InvalidZipEntry.DoubleForwardSlashesInPath;
                }

                ZipArchiveEntry entryWithDoubleBackSlash = archive.Entries.FirstOrDefault(
                    e => e.FullName.Contains("\\\\"));

                if (entryWithDoubleBackSlash != null)
                {
                    entry = entryWithDoubleBackSlash;
                    return InvalidZipEntry.DoubleBackwardSlashesInPath;
                }
            }

            return InvalidZipEntry.None;
        }

        internal static string NormalizeForwardSlashesInPath(string path)
        {
            StringBuilder sb = new StringBuilder();
            bool lastWasSlash = false;

            foreach (char c in path)
            {
                if (c == '/')
                {
                    if (!lastWasSlash)
                    {
                        sb.Append(c);
                        lastWasSlash = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    lastWasSlash = false;
                }

                // Standard ZIP format specification has a limitation for file path lengths of 260 characters
                if (sb.Length >= 260)
                {
                    break;
                }
            }

            return sb.ToString();
        }
    }
}