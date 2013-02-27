using System;
using System.Collections.Generic;
using System.IO;
using NuGet;

namespace NuGetGallery
{
    public interface INupkg : IDisposable
    {
        /// <summary>
        /// Gets the package metadata contained in the nupkg's nuspec file.
        /// </summary>
        IPackageMetadata Metadata { get; }

        /// <summary>
        /// Gets all the paths of all *non-metadata* files contained in the nupkg.
        /// [Content_Types].xml, .rels, .pmd, and .nuspec files will not be included.
        /// </summary>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Gets a decompressed file stream for one of the files in the package, and ensures
        /// that the stream so returned will not read more than maxSize bytes.
        /// </summary>
        Stream GetCheckedFileStream(string filePath, int maxSize);

        /// <summary>
        /// Gets the backing Stream which this package was read from (and seeks stream to position zero).
        /// </summary>
        Stream GetStream();

        IEnumerable<System.Runtime.Versioning.FrameworkName> GetSupportedFrameworks();
    }
}