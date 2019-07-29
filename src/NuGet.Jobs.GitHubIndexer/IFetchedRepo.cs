// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.GitHubIndexer
{
    public interface IFetchedRepo : IDisposable
    {
        /// <summary>
        /// Returns a list of all files in the repository.
        /// </summary>
        /// <returns>List of files in the repository</returns>
        IReadOnlyList<GitFileInfo> GetFileInfos();

        /// <summary>
        /// Persists the specified files to a storage and returns links to those files as a list.
        /// </summary>
        /// <param name="filePaths">Paths to files in the repository</param>
        /// <returns>List of the persisted files.</returns>
        IReadOnlyList<ICheckedOutFile> CheckoutFiles(IReadOnlyCollection<string> filePaths);
    }
}
