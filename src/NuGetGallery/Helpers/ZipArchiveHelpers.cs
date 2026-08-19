// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
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

        /// <summary>
        /// Validates the entries in a package archive and returns a user-facing error message.
        /// </summary>
        /// <param name="stream">The stream containing the package archive.</param>
        /// <returns>An error message when validation fails; otherwise, <see langword="null"/>.</returns>
        public static string GetArchiveValidationError(Stream stream)
        {
            try
            {
                var invalidEntry = ValidateArchiveEntries(stream, out var entry);
                switch (invalidEntry)
                {
                    case InvalidZipEntry.None:
                        return null;
                    case InvalidZipEntry.InFuture:
                        return string.Format(CultureInfo.CurrentCulture, Strings.PackageEntryFromTheFuture, entry.Name);
                    case InvalidZipEntry.DoubleForwardSlashesInPath:
                        return string.Format(CultureInfo.CurrentCulture, Strings.PackageEntryWithDoubleForwardSlash, entry.Name);
                    case InvalidZipEntry.DoubleBackwardSlashesInPath:
                        return string.Format(CultureInfo.CurrentCulture, Strings.PackageEntryWithDoubleBackSlash, entry.Name);
                    default:
                        return string.Format(CultureInfo.CurrentCulture, Strings.InvalidPackageEntry, entry.Name);
                }
            }
            catch (Exception exception)
            {
                exception.Log();
                return Strings.FailedToReadUploadFile;
            }
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