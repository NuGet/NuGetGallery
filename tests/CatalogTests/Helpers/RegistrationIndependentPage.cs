// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;

namespace CatalogTests.Helpers
{
    internal sealed class RegistrationIndependentPage : RegistrationPage
    {
        [JsonProperty(CatalogConstants.ContextKeyword)]
        internal JObject ContextKeyword { get; }

        [JsonConstructor]
        internal RegistrationIndependentPage(
            string idKeyword,
            string typeKeyword,
            string commitId,
            string commitTimeStamp,
            int count,
            RegistrationPackage[] items,
            string parent,
            string lower,
            string upper,
            JObject contextKeyword)
            : base(idKeyword, typeKeyword, commitId, commitTimeStamp, count, items, parent, lower, upper)
        {
            ContextKeyword = contextKeyword;
        }
    }
}