// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class InitialAuxiliaryData
    {
        public InitialAuxiliaryData(
            SortedDictionary<string, SortedSet<string>> owners,
            DownloadData downloads,
            HashSet<string> excludedPackages,
            HashSet<string> verifiedPackages,
            PopularityTransferData popularityTransfers)
        {
            Owners = owners ?? throw new ArgumentNullException(nameof(owners));
            Downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
            ExcludedPackages = excludedPackages ?? throw new ArgumentNullException(nameof(excludedPackages));
            VerifiedPackages = verifiedPackages ?? throw new ArgumentNullException(nameof(verifiedPackages));
            PopularityTransfers = popularityTransfers ?? throw new ArgumentNullException(nameof(popularityTransfers));
        }

        public SortedDictionary<string, SortedSet<string>> Owners { get; }
        public DownloadData Downloads { get; }
        public HashSet<string> ExcludedPackages { get; }
        public HashSet<string> VerifiedPackages { get; }
        public PopularityTransferData PopularityTransfers { get; }
    }
}
