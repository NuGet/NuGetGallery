// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    internal class RegistrationPage
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
        [JsonProperty(CatalogConstants.Items)]
        public RegistrationPackage[] Items { get; protected set; }
        [JsonProperty(CatalogConstants.Parent)]
        public string Parent { get; protected set; }
        [JsonProperty(CatalogConstants.Lower)]
        public string Lower { get; protected set; }
        [JsonProperty(CatalogConstants.Upper)]
        public string Upper { get; protected set; }

        [JsonConstructor]
        internal RegistrationPage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            RegistrationPackage[] items,
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