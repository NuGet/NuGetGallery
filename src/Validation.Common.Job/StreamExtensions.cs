// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Asynchronously reads the bytes from the current stream and writes them to another stream, using a specified
        /// buffer size and cancellation token. The copy operation halts if the number of bytes written exceeds a limit.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write to.</param>
        /// <param name="bufferSize">The desired buffer size in bytes.</param>
        /// <param name="maxCopyBytes">The maximum number of bytes to write to the destination.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A result providing details on the copy operation.</returns>
        public static async Task<StreamCopyResult> CopyToAsync(
            this Stream source,
            Stream destination,
            int bufferSize,
            long maxCopyBytes,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            long totalBytesWritten = 0;

            int actualRead;
            do
            {
                // Read up to one more byte than we intend on ever writing. This allows us to determine if we only read
                // of the stream. This allows the caller to react to a partial read.
                var readUpTo = Math.Min(buffer.Length, (int)((maxCopyBytes + 1) - totalBytesWritten));
                actualRead = await source.ReadAsync(buffer, offset: 0, count: readUpTo, cancellationToken: cancellationToken);
                totalBytesRead += actualRead;

                var write = Math.Min(actualRead, (int)(maxCopyBytes - totalBytesWritten));
                if (write > 0)
                {
                    await destination.WriteAsync(buffer, offset: 0, count: write, cancellationToken: cancellationToken);
                    totalBytesWritten += write;
                }
            }
            while (totalBytesRead <= maxCopyBytes && actualRead > 0);

            return new StreamCopyResult(partialRead: actualRead > 0, bytesWritten: totalBytesWritten);
        }
    }
}
