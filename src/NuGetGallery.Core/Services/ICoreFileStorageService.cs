// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ICoreFileStorageService
    {
        Task DeleteFileAsync(string folderName, string fileName);

        Task<bool> FileExistsAsync(string folderName, string fileName);

        Task<Stream> GetFileAsync(string folderName, string fileName);

        /// <summary>
        /// Gets a reference to a file in the storage service, which can be used to open the full file data.
        /// </summary>
        /// <param name="folderName">The folder containing the file to open</param>
        /// <param name="fileName">The file within that folder to open</param>
        /// <param name="ifNoneMatch">The <see cref="IFileReference.ContentId"/> value to use in an If-None-Match request</param>
        /// <returns>A <see cref="IFileReference"/> representing the file reference</returns>
        Task<IFileReference> GetFileReferenceAsync(string folderName, string fileName, string ifNoneMatch = null);

        /// <summary>
        /// Generates the storage file URI (which is optionally time limited)
        /// </summary>
        /// <param name="folderName">The folder containing the file.</param>
        /// <param name="fileName">The file within the <paramref name="folderName"/>.</param>
        /// <param name="endOfAccess">End of access timestamp. Implementation should produce URIs that will become
        /// invalid after that timestamp if it has support for it. If null then would be no time on URI availability.</param>
        /// <returns>Time limited URI (if requested and implementation supports it) for the specified file.</returns>
        Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess);

        Task SaveFileAsync(string folderName, string fileName, Stream packageFile, bool overwrite = true);

        /// <summary>
        /// Copies the source URI to the destination file. If the destination already exists and the content
        /// is different, an exception should be thrown. If the file already exists, the implementation can choose to
        /// no-op if the content is the same instead of throwing an exception. This method should throw if the source
        /// file does not exist.
        /// </summary>
        /// <param name="srcUri">The URI of the source file.</param>
        /// <param name="destFolderName">The destination folder.</param>
        /// <param name="destFileName">The destination file name or relative file path.</param>
        /// <param name="destAccessCondition">
        /// The access condition used to determine whether the destination is in the expected state.
        /// </param>
        /// <returns>
        /// The etag of the source file. This can be used if the destination file is later intended to replace
        /// the source file in conjunction with <paramref name="destAccessCondition"/>.
        /// </returns>
        Task CopyFileAsync(
            Uri srcUri,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition);

        /// <summary>
        /// Copies the source file to the destination file. If the destination already exists and the content
        /// is different, an exception should be thrown. If the file already exists, the implementation can choose to
        /// no-op if the content is the same instead of throwing an exception. This method should throw if the source
        /// file does not exist.
        /// </summary>
        /// <param name="srcFolderName">The source folder.</param>
        /// <param name="srcFileName">The source file name or relative file path.</param>
        /// <param name="destFolderName">The destination folder.</param>
        /// <param name="destFileName">The destination file name or relative file path.</param>
        /// <param name="destAccessCondition">
        /// The access condition used to determine whether the destination is in the expected state.
        /// </param>
        /// <returns>
        /// The etag of the source file. This can be used if the destination file is later intended to replace
        /// the source file in conjunction with <paramref name="destAccessCondition"/>.
        /// </returns>
        Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition);
    }
}
