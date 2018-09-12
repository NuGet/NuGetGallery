// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class CatalogIndependentPage : CatalogPage
    {
        [JsonProperty(CatalogConstants.Parent)]
        internal string Parent { get; }
        [JsonProperty(CatalogConstants.Items)]
        internal CatalogPackageDetails[] Items { get; }
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        [JsonConstructor]
        internal CatalogIndependentPage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            string parent,
            CatalogPackageDetails[] items,
            JObject contextKeyword)
            : base(idKeyword, typeKeyword, commitId, commitTimeStamp, count)
        {
            Parent = parent;
            Items = items;
            ContextKeyword = contextKeyword;
        }
    }
}