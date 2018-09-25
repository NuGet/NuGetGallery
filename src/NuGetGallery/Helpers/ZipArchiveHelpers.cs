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
        /// <see cref="Stream"/> if it has the entry in the future. It will return
        /// the first entry found in the future.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> object to verify</param>
        /// <param name="entry"><see cref="ZipArchiveEntry"/> found with future entry.</param>
        /// <returns>True if <see cref="Stream"/> contains an entry in future, false otherwise.</returns>
        public static bool FoundEntryInFuture(Stream stream, out ZipArchiveEntry entry)
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
                    return true;
                }
            }

            return false;
        }
    }
}