// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryData : IAuxiliaryData
    {
        public AuxiliaryData(
            DateTimeOffset loaded,
            AuxiliaryFileResult<DownloadData> downloads,
            AuxiliaryFileResult<HashSet<string>> verifiedPackages,
            AuxiliaryFileResult<PopularityTransferData> popularityTransfers)
        {
            Downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
            VerifiedPackages = verifiedPackages ?? throw new ArgumentNullException(nameof(verifiedPackages));
            PopularityTransfers = popularityTransfers ?? throw new ArgumentNullException(nameof(popularityTransfers));

            Metadata = new AuxiliaryFilesMetadata(
                loaded,
                Downloads.Metadata,
                VerifiedPackages.Metadata,
                PopularityTransfers.Metadata);
        }

        internal AuxiliaryFileResult<DownloadData> Downloads { get; }
        internal AuxiliaryFileResult<HashSet<string>> VerifiedPackages { get; }
        internal AuxiliaryFileResult<PopularityTransferData> PopularityTransfers { get; }
        public AuxiliaryFilesMetadata Metadata { get; }

        public bool IsVerified(string id)
        {
            return VerifiedPackages.Data.Contains(id);
        }

        public long GetTotalDownloadCount(string id)
        {
            return Downloads.Data.GetDownloadCount(id);
        }

        public long GetDownloadCount(string id, string normalizedVersion)
        {
            return Downloads.Data.GetDownloadCount(id, normalizedVersion);
        }

        public string[] GetPopularityTransfers(string id)
        {
            if (PopularityTransfers.Data.TryGetValue(id, out var result))
            {
                return result.ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
