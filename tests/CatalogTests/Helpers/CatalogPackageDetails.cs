// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class CatalogPackageDetails
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CommitId)]
        internal string CommitId { get; }
        [JsonProperty(CatalogConstants.CommitTimeStamp)]
        internal string CommitTimeStamp { get; }
        [JsonProperty(CatalogConstants.NuGetId)]
        internal string Id { get; }
        [JsonProperty(CatalogConstants.NuGetVersion)]
        internal string Version { get; }

        [JsonConstructor]
        internal CatalogPackageDetails(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            string id,
            string version)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Id = id;
            Version = version;
        }

        internal static CatalogPackageDetails Create(CatalogIndependentPackageDetails details)
        {
            return new CatalogPackageDetails(
                details.IdKeyword,
                CatalogConstants.NuGetPackageDetails,
                details.CommitId,
                details.CommitTimeStamp,
                details.Id,
                details.Version);
        }
    }
}