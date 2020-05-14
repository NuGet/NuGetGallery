// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    public enum FileDownloadResultType
    {
        /// <summary>
        /// The file was successfully download and the stream is available for consumption.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The file does not exist. The stream is not available for consumption.
        /// </summary>
        NotFound = 1,

        /// <summary>
        /// The file had an unexpected file size. The stream is not available for consumption.
        /// </summary>
        UnexpectedFileSize = 2,
    }
}
