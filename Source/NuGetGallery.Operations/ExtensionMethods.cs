using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace NuGetGallery.Operations
{
    public static class ExtensionMethods
    {
        public static bool AnySafe<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any(predicate);
        }
        
        public static string ToShortNameOrNull(this FrameworkName frameworkName)
        {
            return frameworkName == null ? null : VersionUtility.GetShortFrameworkName(frameworkName);
        }
    }
}
