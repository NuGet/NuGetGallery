// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    public class RegistrationPackageDeprecationAlternatePackage
    {
        [JsonConstructor]
        public RegistrationPackageDeprecationAlternatePackage(
            string id,
            string range)
        {
            Id = id;
            Range = range;
        }

        [JsonProperty(CatalogConstants.Id)]
        public string Id { get; }

        [JsonProperty(CatalogConstants.Range)]
        public string Range { get; }
    }
}
