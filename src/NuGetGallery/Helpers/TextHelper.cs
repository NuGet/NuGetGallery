// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery.Helpers
{
    public static class TextHelper
    {
        /// <summary>
        /// Checks if the stream contains only bytes that are OK to have in a UTF-8 encoded plain text file.
        /// </summary>
        /// <param name="stream">Stream to check.</param>
        /// <param name="bufferSize">The size of the buffer to use for reading.</param>
        /// <returns>True if file looks like proper UTF-8 files. False if file looks like binary file.</returns>
        /// <remarks>
        /// This method read the stream byte by byte and checks if each of them is a "binary" byte (<see cref="IsUtf8TextByte(byte)"/>).
        /// This method does NOT check if the file contains valid UTF-8 sequences. The purpose is to be able to detect obviously binary files.
        /// Method will read from the stream until end of stream is encountered. It is the caller's responsibility to resolve all potential
        /// concerns related to the size of the stream.
        /// </remarks>
        public static async Task<bool> LooksLikeUtf8TextStreamAsync(Stream stream, int bufferSize = 1024)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException($"{nameof(stream)} argument must be a readable stream", nameof(stream));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), $"{nameof(bufferSize)} must be greater than 0");
            }

            var buffer = new byte[bufferSize];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, bufferSize);
                for (int i = 0; i < bytesRead; ++i)
                {
                    if (!IsUtf8TextByte(buffer[i]))
                    {
                        return false;
                    }
                }
            } while (bytesRead > 0);

            return true;
        }

        /// <summary>
        /// Checks if the byte is OK to be present in a UTF-8 encoded plain text file.
        /// </summary>
        /// <param name="byteValue">The byte value to check</param>
        /// <returns>True if the byte is OK to exist in UTF-8 encoded plain text file, false if byte belongs to a binary file.</returns>
        /// <remarks>
        /// All bytes &lt; 32 except tabs and line break characters are considered invalid (not appearing in proper UTF-8 files),
        /// while everything else is allowed.
        /// </remarks>
        public static bool IsUtf8TextByte(byte byteValue)
        {
            const int TextRangeStart = ' ';
            const int LineFeed = '\n';
            const int CarriageReturn = '\r';
            const int Tab = '\t';
            const int FormFeed = '\f';

            return byteValue >= TextRangeStart || byteValue == LineFeed || byteValue == CarriageReturn || byteValue == Tab || byteValue == FormFeed;
        }
    }
}