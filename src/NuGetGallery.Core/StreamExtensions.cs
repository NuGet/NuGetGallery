// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class StreamExtensions
    {
        public static Stream AsSeekableStream(this Stream stream)
        {
            if (stream.CanRead && stream.CanSeek)
            {
                stream.Position = 0;
                return stream;
            }

            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// Checks whether stream's next bytes are the same as <paramref name="expectedBytes"/>.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="expectedBytes">Expected bytes.</param>
        /// <returns>True if it manages to read the same amount of bytes as in <paramref name="expectedBytes"/> and 
        /// the bytes read are the same as in <paramref name="expectedBytes"/> array, false otherwise.</returns>
        /// <remarks>
        /// The method assumes that stream is properly positioned before calling by the caller. It will not seek anywhere.
        /// If the length of <paramref name="expectedBytes"/> is 0, method returns true.
        /// </remarks>
        public static async Task<bool> NextBytesMatchAsync(this Stream stream, byte[] expectedBytes)
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
    }
}
