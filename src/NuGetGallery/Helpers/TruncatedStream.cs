// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGetGallery.Helpers
{
    public class TruncatedStream : IDisposable
    {
        public bool IsTruncated { get; }
        public MemoryStream Stream { get; }
        public TruncatedStream(MemoryStream stream, bool isTruncated)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            Stream = stream;
            IsTruncated = isTruncated;
        }

        public void Dispose()
        {
            Stream.Dispose();
        }
    }
}