using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class DictionaryHelper
    {
        public static TValue GetValueOrDefault<TKey,TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            var ret = default(TValue);
            dictionary.TryGetValue(key, out ret);
            return ret;
        }

        public static IEnumerable<TValue> GetValueOrDefault<TKey,TValue>(this ILookup<TKey, TValue> lookup, TKey key)
        {
            if (lookup.Contains(key))
            {
                return lookup[key];
            }

            return null;
        }
    }
}