// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal class CatalogPage
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        public string IdKeyword { get; protected set; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        public string TypeKeyword { get; protected set; }
        [JsonProperty(CatalogConstants.CommitId)]
        public string CommitId { get; }
        [JsonProperty(CatalogConstants.CommitTimeStamp)]
        public string CommitTimeStamp { get; protected set; }
        [JsonProperty(CatalogConstants.Count)]
        public int Count { get; protected set; }

        [JsonConstructor]
        internal CatalogPage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Count = count;
        }

        internal static CatalogPage Create(CatalogIndependentPage page)
        {
            return new CatalogPage(
                page.IdKeyword,
                page.TypeKeyword,
                page.CommitId,
                page.CommitTimeStamp,
                page.Count);
        }
    }
}