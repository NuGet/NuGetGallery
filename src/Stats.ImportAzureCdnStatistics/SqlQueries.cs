// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class SqlQueries
    {
        private const string _sqlGetAllTimeDimensions = "SELECT [Id], [HourOfDay] FROM [dbo].[Dimension_Time]";
        private const string _sqlGetDateDimensions = "SELECT [Id], [Date] FROM [dbo].[Dimension_Date] WHERE [Date] >= '{0}' AND [Date] <= '{1}'";

        public static string GetAllTimeDimensions()
        {
            return _sqlGetAllTimeDimensions;
        }

        public static string GetDateDimensions(DateTime min, DateTime max)
        {
            return string.Format(_sqlGetDateDimensions, min.ToString("yyyy-MM-dd"), max.ToString("yyyy-MM-dd"));
        }
    }
}