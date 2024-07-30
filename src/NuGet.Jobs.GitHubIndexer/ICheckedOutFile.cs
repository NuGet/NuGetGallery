// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface ICheckedOutFile
    {
        /// <summary>
        /// Opens a Stream to the checkedout file.
        /// </summary>
        /// <returns>Stream containing the file data.</returns>
        Stream OpenFile();

        /// <summary>
        /// Path to the file.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Id of the repository containing the file.
        /// </summary>
        string RepoId { get; }
    }
}
