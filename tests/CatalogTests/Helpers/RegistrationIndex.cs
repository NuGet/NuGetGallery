// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationIndex
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string[] TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CommitId)]
        internal string CommitId { get; }
        [JsonProperty(CatalogConstants.CommitTimeStamp)]
        internal string CommitTimeStamp { get; }
        [JsonProperty(CatalogConstants.Count)]
        internal int Count { get; }
        [JsonProperty(CatalogConstants.Items)]
        internal RegistrationPage[] Items { get; }
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        [JsonConstructor]
        internal RegistrationIndex(
            string idKeyword,
            string[] typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            RegistrationPage[] items,
            JObject contextKeyword)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Count = count;
            Items = items;
            ContextKeyword = contextKeyword;
        }
    }
}