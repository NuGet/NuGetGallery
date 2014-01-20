using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace NuCmd
{
    public interface IConsoleFormatter
    {
        string Format(object target);
    }

    public class DefaultConsoleFormatter : IConsoleFormatter
    {
        public static readonly DefaultConsoleFormatter Instance = new DefaultConsoleFormatter();

        protected DefaultConsoleFormatter() { }

        public string Format(object target)
        {
            return String.Join(
                Environment.NewLine,
                Enumerable.Concat(
                    new [] { target.GetType().FullName },
                    SelectProperties(target)
                        .Select(p => {
                            var val = GetValue(target, p);
                            return " " + p.Name + ": " + RenderValue(val, p, target);
                        })));
        }

        private static string RenderValue(object value, PropertyInfo sourceProperty, object target)
        {
            IDictionary<string, string> dict = value as IDictionary<string, string>;
            if(dict != null)
            {
                value = JsonConvert.SerializeObject(dict);
            }

            return (value == null ? String.Empty : value.ToString());
        }

        private object GetValue(object target, PropertyInfo property)
        {
            return property.GetValue(target);
        }

        public virtual IEnumerable<PropertyInfo> SelectProperties(object target)
        {
            return target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
