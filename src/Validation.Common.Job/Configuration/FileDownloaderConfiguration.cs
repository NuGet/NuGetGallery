// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    public class FileDownloaderConfiguration
    {
        /// <summary>
        /// The size of the buffer used to copy the network stream.
        /// </summary>
        /// <remarks>Implementation uses the <see cref="System.IO.Stream.CopyToAsync(System.IO.Stream, int, System.Threading.CancellationToken)"/>
        /// passing that value as the second argument.
        /// 
        /// Setting the package downloader default buffer size to the buffer size used for copying streams
        /// see https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Stream.cs#L32-L35
        /// </remarks>
        public int BufferSize { get; set; } = 80 * 1024;
    }
}
