// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationIndependentPackage
    {
        [JsonProperty(CatalogConstants.IdKeyword)]
        internal string IdKeyword { get; }
        [JsonProperty(CatalogConstants.TypeKeyword)]
        internal string[] TypeKeyword { get; }
        [JsonProperty(CatalogConstants.CatalogEntry)]
        internal string CatalogEntry { get; }
        [JsonProperty(CatalogConstants.Listed)]
        internal bool Listed { get; }
        [JsonProperty(CatalogConstants.PackageContent)]
        internal string PackageContent { get; }
        [JsonProperty(CatalogConstants.Published)]
        internal string Published { get; }
        [JsonProperty(CatalogConstants.Registration)]
        internal string Registration { get; }
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        [JsonConstructor]
        internal RegistrationIndependentPackage(
            string idKeyword,
            string[] typeKeyword,
            string catalogEntry,
            bool listed,
            string packageContent,
            string published,
            string registration,
            JObject contextKeyword)
        {
            IdKeyword = idKeyword;
            TypeKeyword = typeKeyword;
            CatalogEntry = catalogEntry;
            Listed = listed;
            PackageContent = packageContent;
            Published = published;
            Registration = registration;
            ContextKeyword = contextKeyword;
        }
    }
}