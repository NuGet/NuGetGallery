using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;

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
