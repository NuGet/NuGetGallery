// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationIndexPage
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CommitId)]
        internal string CommitId { get; }
        [JsonProperty(CatalogConstants.CommitTimeStamp)]
        internal string CommitTimeStamp { get; }
        [JsonProperty(CatalogConstants.Count)]
        internal int Count { get; }
        [JsonProperty(CatalogConstants.Items)]
        internal RegistrationIndexPackageDetails[] Items { get; }
        [JsonProperty(CatalogConstants.Parent)]
        internal string Parent { get; }
        [JsonProperty(CatalogConstants.Lower)]
        internal string Lower { get; }
        [JsonProperty(CatalogConstants.Upper)]
        internal string Upper { get; }

        [JsonConstructor]
        internal RegistrationIndexPage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            RegistrationIndexPackageDetails[] items,
            string parent,
            string lower,
            string upper)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Count = count;
            Items = items;
            Parent = parent;
            Lower = lower;
            Upper = upper;
        }
    }
}