// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Xml.Linq;

namespace NuGet.Jobs
{
    public static class XElementExtensions
    {
        public static string GetString(this XElement element, XName name, string defaultValue)
        {
            var childElement = element.Element(name);
            if (childElement != null)
            {
                return childElement.Value;
            }

            return defaultValue;
        }

        public static int GetInt32(this XElement element, XName name, int defaultValue)
        {
            int.TryParse(GetString(element, name, defaultValue.ToString()), out defaultValue);
            return defaultValue;
        }

        public static long GetInt64(this XElement element, XName name, long defaultValue)
        {
            long.TryParse(GetString(element, name, defaultValue.ToString()), out defaultValue);
            return defaultValue;
        }

        public static bool GetBool(this XElement element, XName name, bool defaultValue)
        {
            bool.TryParse(GetString(element, name, defaultValue.ToString()), out defaultValue);
            return defaultValue;
        }

        public static Uri GetUri(this XElement element, XName name, Uri defaultValue)
        {
            var value = GetString(element, name, null);
            if (!string.IsNullOrEmpty(value))
            {
                Uri result = null;
                if (Uri.TryCreate(value, UriKind.Absolute, out result))
                {
                    return result;
                }
            }

            return defaultValue;
        }

        public static DateTimeOffset GetDateTimeOffset(this XElement element, XName name)
        {
            var value = GetDateTimeOffset(element, name, new DateTimeOffset?());
            if (value.HasValue)
            {
                return value.Value;
            }

            return new DateTimeOffset();
        }

        public static DateTimeOffset? GetDateTimeOffset(this XElement element, XName name, DateTimeOffset? defaultValue)
        {
            var childElement = element.Element(name);
            if (childElement != null && !string.IsNullOrEmpty(childElement.Value))
            {
                return DateTimeOffset.Parse(childElement.Value, null, DateTimeStyles.AssumeUniversal);
            }

            return defaultValue;
        }
    }
}