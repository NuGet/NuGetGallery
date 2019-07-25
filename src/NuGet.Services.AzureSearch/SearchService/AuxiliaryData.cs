// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryData : IAuxiliaryData
    {
        public AuxiliaryData(
            AuxiliaryFileResult<Downloads> downloads,
            AuxiliaryFileResult<HashSet<string>> verifiedPackages)
        {
            Downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
            VerifiedPackages = verifiedPackages ?? throw new ArgumentNullException(nameof(verifiedPackages));
            Metadata = new AuxiliaryFilesMetadata(
                Downloads.Metadata,
                VerifiedPackages.Metadata);
        }

        internal AuxiliaryFileResult<Downloads> Downloads { get; }
        internal AuxiliaryFileResult<HashSet<string>> VerifiedPackages { get; }
        public AuxiliaryFilesMetadata Metadata { get; }

        public bool IsVerified(string id)
        {
            return VerifiedPackages.Data.Contains(id);
        }

        public int GetTotalDownloadCount(string id)
        {
            return Downloads.Data[id]?.Total ?? 0;
        }

        public int GetDownloadCount(string id, string normalizedVersion)
        {
            return Downloads.Data[id]?[normalizedVersion] ?? 0;
        }
    }
}
