using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Xml.Linq
{
    internal static class XLinqExtensions
    {
        public static string AttributeValue(this XElement self, XName name)
        {
            return AttributeValue(self, name, null);
        }

        public static string AttributeValue(this XElement self, XName name, string defaultValue)
        {
            var attr = self.Attribute(name);
            if (attr == null)
            {
                return defaultValue;
            }
            return attr.Value;
        }

        public static T AttributeValueAs<T>(this XElement self, XName name, Func<string, T> converter)
        {
            return AttributeValueAs(self, name, converter, default(T));
        }

        public static T AttributeValueAs<T>(this XElement self, XName name, Func<string, T> converter, T defaultValue) {
            string val = AttributeValue(self, name);
            if (String.IsNullOrEmpty(val))
            {
                return defaultValue;
            }
            return converter(val);
        }

        public static T ValueAs<T>(this XElement self, Func<string, T> converter)
        {
            return ValueAs(self, converter, default(T));
        }

        public static T ValueAs<T>(this XElement self, Func<string, T> converter, T defaultValue)
        {
            if (String.IsNullOrEmpty(self.Value))
            {
                return defaultValue;
            }
            return converter(self.Value);
        }
    }
}
