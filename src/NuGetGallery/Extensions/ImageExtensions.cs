// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGetGallery.Helpers;

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
        /// The JPG file header bytes.
        /// </summary>
        /// <remarks>
        /// Technically, JPEG start with two byte SOI (start of image) segment: FFD8, followed by several other segments or fill bytes.
        /// All of the segments start with FF, and fill bytes are FF, so we check the first 3 bytes instead of the first two.
        /// https://www.w3.org/Graphics/JPEG/itu-t81.pdf "B.1.1.2 Markers"
        /// </remarks>
        private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };

        public static async Task<bool> NextBytesMatchPngHeaderAsync(this Stream stream)
        {
            return await stream.NextBytesMatchAsync(PngHeader);
        }

        public static async Task<bool> NextBytesMatchJpegHeaderAsync(this Stream stream)
        {
            return await stream.NextBytesMatchAsync(JpegHeader);
        }

        public static bool StartsWithPngHeader(this byte[] imageData)
        {
            return ArrayStartsWith(imageData, PngHeader);
        }

        public static bool StartsWithJpegHeader(this byte[] imageData)
        {
            return ArrayStartsWith(imageData, JpegHeader);
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