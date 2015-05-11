// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using NuGet;

namespace NuGetGallery.Packaging
{
    public interface INupkg : IDisposable
    {
        /// <summary>
        /// Gets the package metadata contained in the nupkg's nuspec file.
        /// </summary>
        IPackageMetadata Metadata { get; }

        /// <summary>
        /// Gets the part names of all OPC parts contained in the nupkg.
        /// Part names are conceptually like relative paths but they always start with '/'.
        /// </summary>
        IEnumerable<string> Parts { get; }

        /// <summary>
        /// Gets all the paths of all *non-OPC-junk* files contained in the nupkg.
        /// .nuspec file will be included in results
        /// [Content_Types].xml, .rels, .pmd, and files will not be included.
        /// </summary>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Gets a decompressed file stream for one of the files in the package, and ensures
        /// that the stream so returned will not read more than maxSize bytes.
        /// </summary>
        Stream GetSizeVerifiedFileStream(string filePath, int maxSize);

        /// <summary>
        /// Gets the backing Stream which this package was read from (and seeks stream to position zero).
        /// Note: a popular usage error is that the backing stream has already been disposed,
        /// either directly or via disposing this object.
        /// </summary>
        Stream GetStream();

        IEnumerable<System.Runtime.Versioning.FrameworkName> GetSupportedFrameworks();
    }
}