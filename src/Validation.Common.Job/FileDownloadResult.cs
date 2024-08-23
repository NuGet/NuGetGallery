// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.Validation
{
    public class FileDownloadResult : IDisposable
    {
        private FileDownloadResult(FileDownloadResultType type, Stream stream)
        {
            Type = type;
            Stream = stream;
        }

        public static FileDownloadResult Ok(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return new FileDownloadResult(FileDownloadResultType.Ok, stream);
        }

        public static FileDownloadResult NotFound() => new FileDownloadResult(FileDownloadResultType.NotFound, stream: null);
        public static FileDownloadResult UnexpectedFileSize() => new FileDownloadResult(FileDownloadResultType.UnexpectedFileSize, stream: null);

        public Stream GetStreamOrThrow()
        {
            if (Type != FileDownloadResultType.Ok)
            {
                Dispose();
                throw new InvalidOperationException("The file failed to be downloaded: " + Type);
            }

            return Stream;
        }

        public FileDownloadResultType Type { get; }
        public Stream Stream { get; }

        public void Dispose() => Stream?.Dispose();
    }
}
