// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System
{
    public static class DateTimeExtensions
    {
        public static DateTime ForceUtc(this DateTime date)
        {
            if (date.Kind != DateTimeKind.Utc)
            {
                date = new DateTime(date.Ticks, DateTimeKind.Utc);
            }

            return date;
        }
    }
}