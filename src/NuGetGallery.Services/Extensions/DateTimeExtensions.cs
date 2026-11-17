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

        public static string ToNuGetShortDateString(this DateTime self)
        {
            return self.ToString("d", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Treats a database timestamp as UTC without converting its value and formats it
        /// using the round-trip ISO 8601 format.
        /// </summary>
        /// <param name="self">The timestamp whose value is assumed to be UTC, regardless of its <see cref="DateTime.Kind"/>.</param>
        /// <returns>The UTC timestamp in round-trip ISO 8601 format.</returns>
        public static string ToUtcIso8601String(this DateTime self)
        {
            return DateTime.SpecifyKind(self, DateTimeKind.Utc).ToString("O");
        }
    }
}