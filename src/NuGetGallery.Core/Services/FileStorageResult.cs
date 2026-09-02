// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the framework-independent result of a file download request.
    /// </summary>
    public abstract class FileStorageResult
    {
        private FileStorageResult()
        {
        }

        /// <summary>
        /// Represents a file that was not found.
        /// </summary>
        public sealed class NotFound : FileStorageResult
        {
        }

        /// <summary>
        /// Represents a file download that should redirect to another URI.
        /// </summary>
        public sealed class Redirect : FileStorageResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Redirect"/> class.
            /// </summary>
            /// <param name="redirectUri">The URI to which the download request should be redirected.</param>
            public Redirect(Uri redirectUri)
            {
                RedirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
            }

            /// <summary>
            /// Gets the URI to which the download request should be redirected.
            /// </summary>
            public Uri RedirectUri { get; }
        }

        /// <summary>
        /// Represents a file download served from a local file path.
        /// </summary>
        public sealed class FilePath : FileStorageResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FilePath"/> class.
            /// </summary>
            /// <param name="path">The local path of the file.</param>
            /// <param name="contentType">The content type of the file.</param>
            /// <param name="fileDownloadName">The file name presented to the downloader.</param>
            public FilePath(string path, string contentType, string fileDownloadName)
            {
                Path = path ?? throw new ArgumentNullException(nameof(path));
                ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
                FileDownloadName = fileDownloadName ?? throw new ArgumentNullException(nameof(fileDownloadName));
            }

            /// <summary>
            /// Gets the local path of the file.
            /// </summary>
            public string Path { get; }

            /// <summary>
            /// Gets the content type of the file.
            /// </summary>
            public string ContentType { get; }

            /// <summary>
            /// Gets the file name presented to the downloader.
            /// </summary>
            public string FileDownloadName { get; }
        }
    }
}
