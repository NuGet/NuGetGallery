// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public interface IZipArchiveService
    {
        /// <summary>
        /// Reads the list of files from the zip stream.
        /// </summary>
        /// <param name="stream">The zip stream.</param>
        /// <param name="matchingExtensions">A set of matching file extensions to filter the results.</param>
        /// <returns>A list of the full file paths inside the zip stream.</returns>
        List<string> ReadFilesFromZipStream(Stream stream, params string[] matchingExtensions);

        /// <summary>
        /// Extracts the files from the zip stream.
        /// </summary>
        /// <param name="stream">The zip stream.</param>
        /// <param name="targetDirectory">The target diorectoryu where to extract.</param>
        /// <param name="filterFileExtensions">A list of file extension to filter. Only files with extension in this list will be extracted.</param>
        /// <param name="filterFileNames">A collection of full symbol file names to be used for filtering.
        /// For example if the <paramref name="stream"/> contains foo.dll and bar.dll 
        /// and the <paramref name="filterFileNames"/> contains only foo.pdb than only the foo.dll wil be extracted.</param>
        /// <returns>The list of the full paths for the extracted files.</returns>
        List<string> ExtractFilesFromZipStream(Stream stream, string targetDirectory, IEnumerable<string> filterFileExtensions, IEnumerable<string> filterFileNames = null);

        /// <summary>
        /// Validate that a zip stream does not contain zip slip vulnerability.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="streamName">The stream name.</param>
        /// <param name="token">The async operation cancellation token.</param>
        /// <returns>True if the file passed the validation.</returns>
        Task<bool> ValidateZipAsync(Stream stream, string streamName, CancellationToken token);
    }
}
