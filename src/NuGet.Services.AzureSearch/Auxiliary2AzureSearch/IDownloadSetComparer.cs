// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public interface IDownloadSetComparer
    {
        /// <summary>
        /// Compares the old download count data with the new download count data and returns the changes.
        /// </summary>
        /// <param name="oldData">The old data.</param>
        /// <param name="newData">The new data.</param>
        /// <returns>The changes where the key is the package ID and the value is the new download count.</returns>
        SortedDictionary<string, long> Compare(DownloadData oldData, DownloadData newData);
    }
}