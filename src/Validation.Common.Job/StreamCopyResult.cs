// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    public class StreamCopyResult
    {
        public StreamCopyResult(bool partialRead, long bytesWritten)
        {
            PartialRead = partialRead;
            BytesWritten = bytesWritten;
        }

        /// <summary>
        /// True if the copy operation was halted before all of the bytes were read from the source stream.
        /// </summary>
        public bool PartialRead { get; }

        /// <summary>
        /// The total number of bytes written to the destination stream.
        /// </summary>
        public long BytesWritten { get; }
    }
}
