// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Jobs.Validation
{
    public static class FileStreamUtility
    {
        public const int BufferSize = 8192;

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
