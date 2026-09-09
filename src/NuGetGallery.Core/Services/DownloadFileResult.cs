// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public enum DownloadFileResultType
    {
        Redirect,
        LocalFile,
        NotFound,
    }

    public sealed class DownloadFileResult
    {
        private DownloadFileResult(
            DownloadFileResultType type,
            Uri redirectUri,
            string filePath,
            string contentType,
            string fileDownloadName)
        {
            Type = type;
            RedirectUri = redirectUri;
            FilePath = filePath;
            ContentType = contentType;
            FileDownloadName = fileDownloadName;
        }

        public DownloadFileResultType Type { get; }

        public Uri RedirectUri { get; }

        public string FilePath { get; }

        public string ContentType { get; }

        public string FileDownloadName { get; }

        public static DownloadFileResult Redirect(Uri redirectUri)
        {
            if (redirectUri == null)
            {
                throw new ArgumentNullException(nameof(redirectUri));
            }

            return new DownloadFileResult(
                DownloadFileResultType.Redirect,
                redirectUri,
                filePath: null,
                contentType: null,
                fileDownloadName: null);
        }

        public static DownloadFileResult LocalFile(string filePath, string contentType, string fileDownloadName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            if (string.IsNullOrWhiteSpace(fileDownloadName))
            {
                throw new ArgumentNullException(nameof(fileDownloadName));
            }

            return new DownloadFileResult(
                DownloadFileResultType.LocalFile,
                redirectUri: null,
                filePath,
                contentType,
                fileDownloadName);
        }

        public static DownloadFileResult NotFound()
        {
            return new DownloadFileResult(
                DownloadFileResultType.NotFound,
                redirectUri: null,
                filePath: null,
                contentType: null,
                fileDownloadName: null);
        }
    }
}
