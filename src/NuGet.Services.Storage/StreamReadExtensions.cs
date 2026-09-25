// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Provides helpers for reading a requested amount of data from a stream, handling partial reads.
    /// </summary>
    public static class StreamReadExtensions
    {
        /// <summary>
        /// Reads up to the requested number of bytes, stopping when the stream reaches its end.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that receives the data.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to store the data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The total number of bytes read, which may be less than <paramref name="count"/> if the end of the stream is reached.</returns>
        public static int ReadUpTo(this Stream stream, byte[] buffer, int offset, int count)
        {
            ValidateArguments(stream, buffer, offset, count);

            var bytesRead = 0;
            while (bytesRead < count)
            {
                var read = stream.Read(buffer, offset + bytesRead, count - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            return bytesRead;
        }

        /// <summary>
        /// Asynchronously reads up to the requested number of bytes, stopping when the stream reaches its end.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that receives the data.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to store the data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>A task containing the total number of bytes read, which may be less than <paramref name="count"/> if the end of the stream is reached.</returns>
        public static async Task<int> ReadUpToAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            ValidateArguments(stream, buffer, offset, count);

            var bytesRead = 0;
            while (bytesRead < count)
            {
                var read = await stream.ReadAsync(buffer, offset + bytesRead, count - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            return bytesRead;
        }

        /// <summary>
        /// Validates the stream read arguments.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that receives the data.</param>
        /// <param name="offset">The zero-based byte offset in the buffer.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        private static void ValidateArguments(Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Offset and count exceed the buffer length.");
            }
        }
    }
}
