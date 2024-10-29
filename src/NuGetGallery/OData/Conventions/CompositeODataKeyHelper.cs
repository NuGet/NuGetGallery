// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.OData.Conventions
{
    internal static class CompositeODataKeyHelper
    {
        private static readonly char[] KeyTrimChars = { ' ', '\'' };

        public static bool TryEnrichRouteValues(string keyRaw, IDictionary<string, object> routeValues)
        {
            IEnumerable<string> compoundKeyPairs = keyRaw.Split(',');
            if (!compoundKeyPairs.Any())
            {
                return false;
            }

            foreach (var compoundKeyPair in compoundKeyPairs)
            {
                string[] pair = compoundKeyPair.Split('=');
                if (pair.Length != 2)
                {
                    continue;
                }
                var keyName = pair[0].Trim(KeyTrimChars);
                var keyValue = pair[1].Trim(KeyTrimChars);

                routeValues.Add(keyName, keyValue);
            }
            return true;
        }
    }
}