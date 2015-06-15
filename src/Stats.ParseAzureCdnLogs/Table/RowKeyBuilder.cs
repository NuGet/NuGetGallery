// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Stats.ParseAzureCdnLogs
{
    public static class RowKeyBuilder
    {
        private const string _rowKeyFormatString = "{0:d21}-{1}";

        public static string CreateReverseChronological(DateTime dateTime)
        {
            return CreateReverseChronological(dateTime, Guid.NewGuid().ToString("N").ToUpper());
        }

        public static string CreateReverseChronological(DateTime dateTime, string suffix)
        {
            return FormatKey(GetTicksDescending(dateTime), suffix);
        }

        private static string FormatKey(long ticks, string suffix)
        {
            return string.Format(CultureInfo.InvariantCulture, _rowKeyFormatString, ticks, suffix);
        }

        private static long GetTicksDescending(DateTime dateTime)
        {
            return DateTimeOffset.MaxValue.UtcDateTime.Ticks - new DateTimeOffset(dateTime).UtcDateTime.Ticks;
        }
    }
}