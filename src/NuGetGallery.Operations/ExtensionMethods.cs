// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGet.Frameworks;

namespace NuGetGallery.Operations
{
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ICollection<T> self, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                self.Add(item);
            }
        }

        public static bool AnySafe<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any(predicate);
        }

        public static string ToShortNameOrNull(this NuGetFramework frameworkName)
        {
            if (frameworkName == null)
            {
                return null;
            }

            var shortFolderName = frameworkName.GetShortFolderName();

            // If the shortFolderName is "any", we want to return null to preserve NuGet.Core
            // compatibility in the V2 feed.
            if (String.Equals(shortFolderName, "any", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return shortFolderName;
        }

        public static string ToFriendlyDateTimeString(this DateTime self)
        {
            return self.ToString("yyyy-MM-dd h:mm tt");
        }
    }
}
