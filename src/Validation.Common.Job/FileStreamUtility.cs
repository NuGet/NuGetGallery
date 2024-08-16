// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Jobs.Validation
{
    public static class FileStreamUtility
    {
        /// <summary>
        /// The buffer size to use for file operations.
        /// </summary>
        /// <remarks>
        /// The value is chosen to align with the default Stream buffer size:
        /// https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Stream.cs#L32-L35
        /// </remarks>
        private const int BufferSize = 80 * 1024;

        public static FileStream GetTemporaryFile()
        {
            return new FileStream(
                Path.GetTempFileName(),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        }

        public static FileStream OpenTemporaryFile(string fileName)
            => new FileStream(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                BufferSize,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }
}
