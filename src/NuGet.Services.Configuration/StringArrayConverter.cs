// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.ComponentModel;
using System.Globalization;

namespace NuGet.Services.Configuration
{
    public class StringArrayConverter : ArrayConverter
    {
        private static readonly string[] EmptyArray = new string[0];

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
            var s = value as string;
            if (s != null)
            {
                if (s == string.Empty)
                {
                    return EmptyArray;
                }
                else
                {
                    return s.Split(';');
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
