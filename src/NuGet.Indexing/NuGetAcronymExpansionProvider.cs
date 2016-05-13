// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Indexing
{
    /// <summary>
    /// This class loads a file that contains the acronyms we want to expand.
    /// 
    /// This file could look like:
    /// 
    /// {
    ///     "ef": [
    ///         "ef",
    ///         "entity framework"
    ///     ]
    /// }
    /// 
    /// The above file expands "ef" into both "ef" and "entity framework".
    /// 
    /// The acronym itself must be repeated in the list of expansions. This
    /// approach allows us to "replace" acronyms as well. For example, imagine
    /// we want to treat "mvc5" as just "mvc". the expansion could look like:
    /// 
    /// {
    ///     "mvc5": [
    ///         "mvc"
    ///     ]
    /// }
    /// </summary>
    public class NuGetAcronymExpansionProvider : IAcronymExpansionProvider
    {
        public static readonly IAcronymExpansionProvider Instance = new NuGetAcronymExpansionProvider();

        private static readonly Dictionary<string, string[]> Acronyms;

        private NuGetAcronymExpansionProvider()
        {
        }

        static NuGetAcronymExpansionProvider()
        {
            var assembly = typeof(NuGetAcronymExpansionProvider).Assembly;
            var assemblyName = assembly.GetName().Name;

            using (var stream = assembly.GetManifestResourceStream(assemblyName + ".Resources.Acronyms.json"))
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = new JsonSerializer();
                Acronyms = serializer.Deserialize<Dictionary<string, string[]>>(reader);
            }
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