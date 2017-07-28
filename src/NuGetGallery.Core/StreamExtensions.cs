// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

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
    }
}