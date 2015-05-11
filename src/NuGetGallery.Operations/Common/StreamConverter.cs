// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel;
using System.IO;

namespace NuGetGallery.Operations.Common
{
    public class FileStreamConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            string s = value as string;
            if (s != null)
            {
                return File.Open(s, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            return null;
        }
    }
}
