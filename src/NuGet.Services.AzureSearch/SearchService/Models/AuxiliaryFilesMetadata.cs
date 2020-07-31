// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Services.AzureSearch.AuxiliaryFiles;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryFilesMetadata
    {
        [JsonConstructor]
        public AuxiliaryFilesMetadata(
            DateTimeOffset loaded,
            AuxiliaryFileMetadata downloads,
            AuxiliaryFileMetadata verifiedPackages,
            AuxiliaryFileMetadata popularityTransfers)
        {
            Loaded = loaded;
            Downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
            VerifiedPackages = verifiedPackages ?? throw new ArgumentNullException(nameof(verifiedPackages));
            PopularityTransfers = popularityTransfers ?? throw new ArgumentNullException(nameof(popularityTransfers));
        }

        public DateTimeOffset Loaded { get; }
        public AuxiliaryFileMetadata Downloads { get; }
        public AuxiliaryFileMetadata VerifiedPackages { get; }
        public AuxiliaryFileMetadata PopularityTransfers { get; }
    }
}
