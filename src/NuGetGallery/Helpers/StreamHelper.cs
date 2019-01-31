// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Helpers
{
    public class StreamHelper
    {
        private const long BufferSize = 80 * 1024;  // 80 KB

        /// <summary>
        /// Transform the stream to string given maximum size.
        /// </summary>
        /// <param name="stream">stream to be read.</param>
        /// <param name="maxSize">maximum size.</param>
        /// <returns>string format of the stream.</returns>
        public static async Task<string> ReadMaxAsync(Stream stream, long maxSize)
        {
            return await ReadMaxAsync(stream, maxSize, encoding: null);
        }

        /// <summary>
        /// Transform the stream to string given maximum size and encoding format.
        /// </summary>
        /// <param name="stream">stream to be read.</param>
        /// <param name="maxSize">maximum size.</param>
        /// <param name="encoding">encoding format.</param>
        /// <returns>string format of the stream.</returns>
        public static async Task<string> ReadMaxAsync(Stream stream, long maxSize, Encoding encoding)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            using (var memoryStream = new MemoryStream())
            {
                int bytesRead;
                var totalBytesRead = 0;
                var buffer = new byte[BufferSize];
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxSize)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                            Strings.StreamMaxLengthExceeded, maxSize));
                    }

                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                }

                return encoding.GetString(memoryStream.ToArray());
            }
        }
    }
}