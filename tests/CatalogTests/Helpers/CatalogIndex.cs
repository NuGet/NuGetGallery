// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class CatalogIndex
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
        [JsonProperty(CatalogConstants.NuGetLastCreated)]
        internal string LastCreated { get; }
        [JsonProperty(CatalogConstants.NuGetLastDeleted)]
        internal string LastDeleted { get; }
        [JsonProperty(CatalogConstants.NuGetLastEdited)]
        internal string LastEdited { get; }
        [JsonProperty(CatalogConstants.Items)]
        internal CatalogPage[] Items { get; }
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        [JsonConstructor]
        internal CatalogIndex(
            string idKeyword,
            string[] typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            string lastCreated,
            string lastDeleted,
            string lastEdited,
            CatalogPage[] items,
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

        internal static CatalogIndex Create(CatalogIndependentPage page, JObject contextKeyword)
        {
            var lastCreated = page.CommitTimeStamp;
            var lastDeleted = page.CommitTimeStamp;
            var lastEdited = page.CommitTimeStamp;
            var pages = new[] { CatalogPage.Create(page) };

            return new CatalogIndex(
                page.Parent,
                new[] { CatalogConstants.CatalogRoot, CatalogConstants.AppendOnlyCatalog, CatalogConstants.Permalink },
                page.CommitId,
                page.CommitTimeStamp,
                pages.Length,
                lastCreated,
                lastDeleted,
                lastEdited,
                pages,
                contextKeyword);
        }
    }
}