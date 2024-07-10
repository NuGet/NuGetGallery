// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        /// Gets a file URI.
        /// </summary>
        /// <param name="folderName">The folder containing the file.</param>
        /// <param name="fileName">The file within the <paramref name="folderName"/>.</param>
        /// <returns>A <see cref="Uri"/> for the specified file.</returns>
        Task<Uri> GetFileUriAsync(string folderName, string fileName);

        /// <summary>
        /// Generates the storage file URI (which is optionally time limited)
        /// </summary>
        /// <param name="folderName">The folder containing the file.</param>
        /// <param name="fileName">The file within the <paramref name="folderName"/>.</param>
        /// <param name="endOfAccess">End of access timestamp. Implementation should produce URIs that will become
        /// invalid after that timestamp if it has support for it. If null then would be no time on URI availability.</param>
        /// <returns>Time limited URI (if requested and implementation supports it) for the specified file.</returns>
        Task<Uri> GetFileReadUriAsync(string folderName, string fileName, DateTimeOffset? endOfAccess);

        /// <summary>
        /// Generates a storage file URI giving certain permissions for the specific file. For example, this method can
        /// be used to generate a URI that allows the caller to either delete (via
        /// <see cref="FileUriPermissions.Delete"/>) or read (via <see cref="FileUriPermissions.Read"/>) the file.
        /// </summary>
        /// <param name="folderName">The folder name containing the file.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="permissions">The permissions to give to the privileged URI.</param>
        /// <param name="endOfAccess">The time when the access ends.</param>
        /// <returns>The URI with privileged access.</returns>
        Task<Uri> GetPrivilegedFileUriAsync(
            string folderName,
            string fileName,
            FileUriPermissions permissions,
            DateTimeOffset endOfAccess);

        Task SaveFileAsync(string folderName, string fileName, Stream file, bool overwrite = true);

        /// <summary>
        /// Saves the file. If storage supports setting the content type for the file,
        /// it will be set to the specified value
        /// </summary>
        /// <param name="folderName">The folder that will contain the file.</param>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="contentType">The content type to set for the saved file if storage supports it.</param>
        /// <param name="file">The content that should be saved.</param>
        /// <param name="overwrite">Indicates whether file should be overwritten if exists.</param>
        /// <exception cref="FileAlreadyExistsException">
        /// Thrown when <paramref name="overwrite"/> is false and file already exists
        /// in destination.
        /// </exception>
        Task SaveFileAsync(string folderName, string fileName, string contentType, Stream file, bool overwrite = true);

        /// <summary>
        /// Saves the file. An exception should be thrown if the access condition is not met.
        /// </summary>
        /// <param name="folderName">The folder that contains the file.</param>
        /// <param name="fileName">The name of file or relative file path.</param>
        /// <param name="file">The content that should be saved to the file.</param>
        /// <param name="accessCondition">The condition used to determine whether to persist the save operation.</param>
        /// <returns>A task that completes once the file is saved.</returns>
        Task SaveFileAsync(string folderName, string fileName, Stream file, IAccessCondition accessCondition);

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

        /// <summary>
        /// Updates metadata on the file.
        /// </summary>
        /// <param name="folderName">The folder name.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="updateMetadataAsync">A function that will update file metadata.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SetMetadataAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, IDictionary<string, string>, Task<bool>> updateMetadataAsync);

        /// <summary>
        /// Updates properties on the file.
        /// </summary>
        /// <param name="folderName">The folder name.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="updatePropertiesAsync">A function that will update file properties.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SetPropertiesAsync(
            string folderName,
            string fileName,
            Func<Lazy<Task<Stream>>, ICloudBlobProperties, Task<bool>> updatePropertiesAsync);

        /// <summary>
        /// Returns the etag value for the specified blob. If the blob does not exists it will return null.
        /// </summary>
        /// <param name="folderName">The folder name.</param>
        /// <param name="fileName">The file name.</param>
        /// <returns>The etag of the specified file.</returns>
        Task<string> GetETagOrNullAsync(
            string folderName,
            string fileName);
    }
}