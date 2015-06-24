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

        public static string ToJavaScriptUTC(this DateTime self)
        {
            return self.ToUniversalTime().ToString("O", CultureInfo.CurrentCulture);
        }

        public static string ToNuGetShortDateTimeString(this DateTime self)
        {
            return self.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        }

        public static string ToNuGetShortDateString(this DateTime self)
        {
            return self.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
        }

        public static string ToNuGetLongDateString(this DateTime self)
        {
            return self.ToString("dddd, MMMM dd yyyy", CultureInfo.CurrentCulture);
        }
    }
}