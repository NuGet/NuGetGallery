// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace NuGetGallery.Helpers
{
    public static class TextHelper
    {
        /// <summary>
        /// Checks if the stream contains only bytes that are OK to have in a UTF-8 encoded plain text file.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        /// <remarks>
        /// This method does NOT check if file contains valid UTF-8 sequences. The purpose is to be able to detect obviously binary files.
        /// Method will read from the stream until end of stream is encountered. It is caller's responsibility to resolve all potential
        /// concerns related to the size of the stream.
        /// </remarks>
        public static bool IsUtf8TextStream(Stream stream, int bufferSize = 1024)
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

            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            do
            {
                bytesRead = stream.Read(buffer, 0, bufferSize);
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
        public static bool IsUtf8TextByte(byte byteValue)
        {
            const int TextRangeStart = ' ';
            const int LineFeed = '\n';
            const int CarriageReturn = '\r';
            const int Tab = '\t';

            return byteValue >= TextRangeStart || byteValue == LineFeed || byteValue == CarriageReturn || byteValue == Tab;
        }
    }
}