// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace NuGetGallery.Authentication
{
    public class ScopeSubjectTypeConverter : TypeConverter
    {
        private static IEnumerable<Type> ConvertibleTypes = new[] 
        {
            typeof(PackageRegistration),
            typeof(Package),
            typeof(ActionOnNewPackageContext)
        };

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (ConvertibleTypes.Any(t => sourceType == t))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is PackageRegistration pr)
            {
                return pr.Id;
            }

            if (value is Package p)
            {
                return ConvertFrom(context, culture, p.PackageRegistration);
            }

            if (value is ActionOnNewPackageContext c)
            {
                return c.PackageId;
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}