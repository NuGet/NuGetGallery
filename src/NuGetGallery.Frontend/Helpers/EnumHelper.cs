using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;

namespace NuGetGallery.Helpers
{
    public static class EnumHelper
    {
        private static readonly ConcurrentDictionary<Type, IDictionary<object, string>> _descriptionMap = new ConcurrentDictionary<Type, IDictionary<object, string>>();

        public static string GetDescription<TEnum>(TEnum value) where TEnum : struct
        {
            Debug.Assert(typeof(TEnum).IsEnum); // Can't encode this in a generic constraint :(

            var descriptions = _descriptionMap.GetOrAdd(typeof(TEnum), key =>
                typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f =>
                {
                    var v = f.GetValue(null);
                    DescriptionAttribute attr = f.GetCustomAttribute<DescriptionAttribute>();

                    string description;
                    if (attr != null)
                    {
                        description = attr.Description;
                    }
                    else
                    {
                        description = v.ToString();
                    }
                    return new KeyValuePair<object, string>(v, description);
                }).ToDictionary(p => p.Key, p => p.Value));

            string desc;
            if (descriptions == null || !descriptions.TryGetValue(value, out desc))
            {
                return value.ToString();
            }
            else
            {
                return desc;
            }
        }
    }
}