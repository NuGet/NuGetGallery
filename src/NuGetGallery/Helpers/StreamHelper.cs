// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery.Helpers
{
    public static class StreamHelper
    {
        private const int BufferSize = 80 * 1024;  // 80 KB

        /// <summary>
        /// Get the truncated memorystream and check whether the input stream exceeds the maxSize.
        /// </summary>
        /// <param name="stream">stream to be read.</param>
        /// <param name="maxSize">maximum size.</param>
        /// <returns>truncated memorystream.</returns>
        public static async Task<TruncatedStream> GetTruncatedStreamWithMaxSizeAsync(this Stream stream, int maxSize)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (maxSize <= 0 || maxSize >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), $"{nameof(maxSize)} must be greater than 0 and less than int.MaxValue");
            }

            var memoryStream = new MemoryStream();
            try
            {
                int bytesRead;
                var totalBytesRead = 0;
                var buffer = new byte[BufferSize];
                while ((bytesRead = await stream.ReadAsync(buffer, 0, GetNeededBytesToRead(totalBytesRead, maxSize))) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxSize)
                    {
                        await memoryStream.WriteAsync(buffer, 0, bytesRead - 1);
                        return new TruncatedStream(memoryStream, isTruncated: true);
                    }
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                }

                return new TruncatedStream(memoryStream, isTruncated: false);
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Checks whether stream's next bytes are the same as <paramref name="expectedBytes"/>.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="expectedBytes">Expected bytes.</param>
        /// <returns>True if the bytes read from stream are the same as in <paramref name="expectedBytes"/> array, false otherwise.</returns>
        public static async Task<bool> StartsWithAsync(this Stream stream, byte[] expectedBytes)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (expectedBytes == null)
            {
                throw new ArgumentNullException(nameof(expectedBytes));
            }

            if (expectedBytes.Length == 0)
            {
                return true;
            }

            var actualBytes = new byte[expectedBytes.Length];
            var bytesRead = await stream.ReadAsync(actualBytes, 0, actualBytes.Length);

            if (bytesRead != expectedBytes.Length)
            {
                return false;
            }

            return expectedBytes.SequenceEqual(actualBytes);
        }

        private static int GetNeededBytesToRead(int totalBytesRead, int maxSize)
        {
            var neededReadSize = maxSize - totalBytesRead + 1;
            if (neededReadSize < BufferSize)
            {
                return neededReadSize;
            }
            else
            {
                return BufferSize;
            }
        }
    }
}