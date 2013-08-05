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
