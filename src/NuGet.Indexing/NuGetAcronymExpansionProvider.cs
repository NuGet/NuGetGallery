// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Indexing.Properties;

namespace NuGet.Indexing
{
    public class NuGetAcronymExpansionProvider : IAcronymExpansionProvider
    {
        public static readonly IAcronymExpansionProvider Instance = new NuGetAcronymExpansionProvider();

        private static readonly Dictionary<string, string[]> Acronyms;

        private NuGetAcronymExpansionProvider()
        {
        }

        static NuGetAcronymExpansionProvider()
        {
            Acronyms = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(
                Strings.NuGetAcronymExpansions);
        }

        public IEnumerable<string> GetKnownAcronyms()
        {
            return Acronyms.Keys;
        }

        public IEnumerable<string> Expand(string acronym)
        {
            string[] expanded;
            if (Acronyms.TryGetValue(acronym, out expanded))
            {
                return expanded;
            }

            return Enumerable.Empty<string>();
        }
    }
}