using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NuCmd
{
    public interface IConsoleFormatter
    {
        string Format(object obj);
    }

    public class DefaultConsoleFormatter : IConsoleFormatter
    {
        public static readonly DefaultConsoleFormatter Instance = new DefaultConsoleFormatter();

        private DefaultConsoleFormatter() { }

        public string Format(object obj)
        {
            return String.Join(
                Environment.NewLine,
                Enumerable.Concat(
                    new [] { obj.GetType().FullName },
                    obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => {
                        var val = p.GetValue(obj);
                        return " " + p.Name + ": " + (val == null ? String.Empty : val.ToString());
                    })));
        }
    }
}
