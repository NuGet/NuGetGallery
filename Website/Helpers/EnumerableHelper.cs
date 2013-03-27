using System.Collections.Generic;

namespace NuGetGallery
{
    public static class EnumerableHelper
    {
        public static ISet<T> ToSet<T>(this IEnumerable<T> items)
        {
            return items as ISet<T> ?? new HashSet<T>(items);
        }
    }
}