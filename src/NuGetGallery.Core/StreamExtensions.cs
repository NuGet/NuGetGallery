// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class StreamExtensions
    {
        public static Stream AsSeekableStream(this Stream stream)
        {
            if (stream.CanRead && stream.CanSeek)
            {
                stream.Position = 0;
                return stream;
            }

            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public static bool FoundEntryInFuture(this Stream stream, out ZipArchiveEntry entry)
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