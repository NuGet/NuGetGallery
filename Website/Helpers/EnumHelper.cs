using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Web;

namespace NuGetGallery.Helpers
{
    public static class EnumHelper
    {
        private static readonly ConcurrentDictionary<Tuple<Type, string>, string> _descriptionMap = new ConcurrentDictionary<Tuple<Type, string>, string>();

        public static string GetEnumDescription<TEnum>(TEnum value)
        {
            return _descriptionMap.GetOrAdd(Tuple.Create(typeof(TEnum), value.ToString()), key =>
            {
                FieldInfo fi = typeof(TEnum).GetType().GetField(value.ToString());
                DescriptionAttribute attr = fi.GetCustomAttribute<DescriptionAttribute>();
                if (attr != null)
                {
                    return attr.Description;
                }
                else
                {
                    return value.ToString();
                }
            });
        }
    }
}