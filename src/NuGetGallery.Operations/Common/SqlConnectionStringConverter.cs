// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;

namespace NuGetGallery.Operations.Common
{
    class SqlConnectionStringConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string s = value as string;
            if (s != null)
            {
                return new SqlConnectionStringBuilder(s);
            }
            return null;
        }
    }
}
