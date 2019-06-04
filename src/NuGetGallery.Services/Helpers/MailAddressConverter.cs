﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Configuration
{
    public class MailAddressConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            string strValue = value as string;
            if (strValue == null)
            {
                return null;
            }
            return new MailAddress(strValue);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            MailAddress srcValue = value as MailAddress;
            if (srcValue != null && destinationType == typeof(string))
            {
                return String.Format(CultureInfo.CurrentCulture, "{0} <{1}>", srcValue.DisplayName, srcValue.Address);
            }
            return null;
        }
    }
}
