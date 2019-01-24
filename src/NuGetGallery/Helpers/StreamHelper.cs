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
        /// <summary>
        /// Trandform the stream to string given maximum size.
        /// </summary>
        /// <param name="stream">stream to be read.</param>
        /// <param name="maxSize">maximum size.</param>
        /// <returns>string format of the stream.</returns>
        public static async Task<string> ReadMaxAsync(Stream stream, long maxSize)
        {
            return await ReadMaxAsync(stream, maxSize, null);
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

            int bytesRead;
            var offset = 0;
            var buffer = new byte[maxSize + 1];

            while ((bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset)) > 0)
            {
                offset += bytesRead;

                if (offset == buffer.Length)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.StreamMaxLengthExceeded, maxSize));
                }
            }

            return encoding.GetString(buffer).Trim('\0');
        }
    }
}