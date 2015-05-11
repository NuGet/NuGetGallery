// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class StatisticsMonthlyUsageItem
    {
        public int Year { get; set; }
        public int MonthOfYear { get; set; }
        public int Downloads { get; set; }
    }
}