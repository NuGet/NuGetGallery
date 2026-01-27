// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace NuGet.Services.Configuration
{
    public class StringArrayConverter : ArrayConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is null)
            {
                return Array.Empty<string>();
            }

            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return Array.Empty<string>();
                }
                else
                {
                    return s.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
