// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGetGallery
{
    public static class DateTimeExtensions
    {
        public static bool IsInThePast(this DateTime? date)
        {
            return date.Value.IsInThePast();
        }

        public static bool IsInThePast(this DateTime date)
        {
            return date < DateTime.UtcNow;
        }

        public static string ToNuGetShortDateString(this DateTimeOffset self)
        {
            return self.ToString("d", CultureInfo.CurrentCulture);
        }

        public static string ToNuGetShortDateString(this DateTime self)
        {
            return self.ToString("d", CultureInfo.CurrentCulture);
        }
    }
}