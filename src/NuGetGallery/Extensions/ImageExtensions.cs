// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Documents;

namespace NuGetGallery
{
    public static class ImageExtensions
    {
        /// <summary>
        /// The PNG file header bytes. All PNG files are expected to have those at the beginning of the file.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/TR/PNG/#5PNG-file-signature
        /// </remarks>
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// The JPG file heder bytes.
        /// </summary>
        /// <remarks>
        /// Technically, JPEG start with SOI (start of image) segment: FFD8, followed by several other segments, but all of them
        /// start with FF, so we check first 3 bytes instead of the first two.
        /// http://www.digicamsoft.com/itu/itu-t81-36.html
        /// </remarks>
        private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };

        public static async Task<bool> HasPngHeaderAsync(this Stream stream)
        {
            return await StreamStartsWithAsync(stream, PngHeader);
        }

        public static async Task<bool> HasJpegHeaderAsync(this Stream stream)
        {
            return await StreamStartsWithAsync(stream, JpegHeader);
        }

        public static bool HasPngHeader(this byte[] imageData)
        {
            return ArrayStartsWith(imageData, PngHeader);
        }

        public static bool HasJpegHeader(this byte[] imageData)
        {
            return ArrayStartsWith(imageData, JpegHeader);
        }

        private static async Task<bool> StreamStartsWithAsync(Stream stream, byte[] expectedBytes)
        {
            var actualBytes = new byte[expectedBytes.Length];
            var bytesRead = await stream.ReadAsync(actualBytes, 0, actualBytes.Length);

            if (bytesRead != expectedBytes.Length)
            {
                return false;
            }

            return expectedBytes.SequenceEqual(actualBytes);
        }

        private static bool ArrayStartsWith(byte[] array, byte[] expectedBytes)
        {
            if (array.Length < expectedBytes.Length)
            {
                return false;
            }

            for (int index = 0; index < expectedBytes.Length; ++index)
            {
                if (array[index] != expectedBytes[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}