// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Helpers
{
    public class StreamHelper
    {
        public static int BufferSize { get; } = 80 * 1024;  // 80 KB

        /// <summary>
        /// Read the stream at most maxSize bytes and transform to string.
        /// </summary>
        /// <param name="stream">stream to be read.</param>
        /// <param name="maxSize">maximum size.</param>
        /// <param name="encoding">encoding format.</param>
        /// <returns>string format of the stream.</returns>
        public static async Task<string> ReadMaxAsync(Stream stream, long maxSize, Encoding encoding = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), $"{nameof(maxSize)} must be greater than 0");
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            using (var memoryStream = new MemoryStream())
            {
                int bytesRead;
                long totalBytesRead = 0;
                var buffer = new byte[GetNeededBufferSize(totalBytesRead, maxSize)];
                while (totalBytesRead < maxSize &&
                       (bytesRead = await stream.ReadAsync(buffer, 0, GetNeededBufferSize(totalBytesRead, maxSize))) > 0)
                {
                    totalBytesRead += bytesRead;
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                }

                return encoding.GetString(memoryStream.ToArray());
            }
        }

        private static int GetNeededBufferSize(long totalBytesRead, long maxSize)
        {
            if (maxSize <= int.MaxValue && maxSize <= BufferSize)
            {
                return (int)maxSize;
            }

            if (maxSize - totalBytesRead <= int.MaxValue && maxSize - totalBytesRead <= BufferSize)
            {
                return (int)(maxSize - totalBytesRead);
            }

            return BufferSize;
        }
    }
}