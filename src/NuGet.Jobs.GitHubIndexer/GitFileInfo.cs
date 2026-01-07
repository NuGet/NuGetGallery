// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitFileInfo
    {
        public GitFileInfo(string path, long blobSize)
        {
            Path = path;
            BlobSize = blobSize;
        }

        public string Path { get; }
        public long BlobSize { get; }
    }
}
