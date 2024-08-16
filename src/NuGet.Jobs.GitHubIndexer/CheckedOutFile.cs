// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.GitHubIndexer
{
    public class CheckedOutFile : ICheckedOutFile
    {
        public CheckedOutFile(string filePath, string repoId)
        {
            Path = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RepoId = repoId ?? throw new ArgumentNullException(nameof(repoId));
        }

        public string Path { get; }
        public string RepoId { get; }
        
        public Stream OpenFile()
        {
            return new FileStream(Path, FileMode.Open);
        }
    }
}
