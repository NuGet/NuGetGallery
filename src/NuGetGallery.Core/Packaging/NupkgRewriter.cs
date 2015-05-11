// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet;

namespace NuGetGallery.Packaging
{
    public class NupkgRewriter
    {
        /// <summary>
        /// Given the nupkg file as a read-write stream with random access (e.g. MemoryStream),
        /// This will replace the .nuspec file with a new .nuspec generated from 
        /// a) the old .nuspec file
        /// b) supplied edits
        /// 
        /// This function leaves readWriteStream open.
        /// </summary>
        public static void RewriteNupkgManifest(Stream readWriteStream, IEnumerable<Action<ManifestMetadata>> edits)
        {
            if (!readWriteStream.CanRead)
            {
                throw new ArgumentException("Must be a readable stream", "readWriteStream");
            }

            if (!readWriteStream.CanWrite)
            {
                throw new ArgumentException("Must be a writeable stream", "readWriteStream");
            }

            if (!readWriteStream.CanSeek)
            {
                throw new ArgumentException("Must be a seekable stream", "readWriteStream");
            }

            Manifest manifest = Nupkg.SafelyLoadManifest(readWriteStream, leaveOpen: true);
            foreach (var edit in edits)
            {
                edit.Invoke(manifest.Metadata);
            }

            using (var newManifestStream = new MemoryStream())
            {
                manifest.Save(newManifestStream);
                using (var archive = new ZipArchive(readWriteStream, ZipArchiveMode.Update, leaveOpen: true))
                {
                    var manifestEntry = archive.Entries.SingleOrDefault(entry =>
                            entry.Name.IndexOf("/", StringComparison.OrdinalIgnoreCase) == -1
                            && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                        );

                    using (var manifestOutputStream = manifestEntry.Open())
                    {
                        manifestOutputStream.SetLength(0);
                        newManifestStream.Position = 0;
                        newManifestStream.CopyTo(manifestOutputStream);
                    }
                }
            }
        }
    }
}