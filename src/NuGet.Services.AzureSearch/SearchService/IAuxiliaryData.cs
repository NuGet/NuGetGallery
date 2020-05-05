// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.SearchService
{
    public interface IAuxiliaryData
    {
        AuxiliaryFilesMetadata Metadata { get; }
        long GetDownloadCount(string id, string normalizedVersion);
        long GetTotalDownloadCount(string id);
        bool IsVerified(string id);
        string[] GetPopularityTransfers(string id);
    }
}