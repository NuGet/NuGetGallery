// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGetGallery
{
    public class StatisticsPackagesViewModel
    {
        public enum WeekFormats
        {
            StartOnly,
            EndOnly,
            FullDate,
            YearWeekNumber
        }

        private static readonly string[] _magnitudeAbbreviations = new string[] { "", "k", "M", "B", "T", "q", "Q", "s", "S", "o", "n" };

        private DateTime? _lastUpdatedUtc;

        public StatisticsPackagesViewModel()
        {
        }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesSummary { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsSummary { get; set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesSummary { get; set; }
        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsSummary { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackagesAll { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadPackageVersionsAll { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackagesAll { get; set; }

        public IEnumerable<StatisticsPackagesItemViewModel> DownloadCommunityPackageVersionsAll { get; set; }

        public IEnumerable<StatisticsNuGetUsageItem> NuGetClientVersion { get; set; }

        public IEnumerable<StatisticsWeeklyUsageItem> Last6Weeks { get; set; }

        public bool IsDownloadPackageAvailable { get; set; }

        public bool IsDownloadPackageVersionsAvailable { get; set; }
        
        public bool IsDownloadCommunityPackageAvailable { get; set; }

        public bool IsDownloadCommunityPackageVersionsAvailable { get; set; }

        public bool IsNuGetClientVersionAvailable { get; set; }

        public bool IsLast6WeeksAvailable { get; set; }

        public long NuGetClientVersionTotalDownloads { get; private set; }

        public string PackageId { get; private set; }

        public string PackageVersion { get; private set; }

        public bool UseD3 { get; set; }

        public DateTime? LastUpdatedUtc
        {
            get { return _lastUpdatedUtc; }
            set { _lastUpdatedUtc = value; }
        }

        public void SetPackageDownloadsByVersion(string packageId)
        {
            PackageId = packageId;
        }

        public void SetPackageVersionDownloadsByClient(string packageId, string packageVersion)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        public string DisplayDownloads(long downloads)
        {
            return downloads.ToNuGetNumberString();
        }

        public void Update()
        {
            if (IsNuGetClientVersionAvailable)
            {
                NuGetClientVersionTotalDownloads = NuGetClientVersion.Sum(item => item.Downloads);
            }
        }

        public string DisplayWeek(int year, int weekOfYear, WeekFormats format = WeekFormats.FullDate)
        {
            if (weekOfYear < 1 || weekOfYear > 53)
            {
                return string.Empty;
            }

            var outputStringTemplate = "";
            switch(format)
            {
                case WeekFormats.YearWeekNumber:
                    return string.Format(CultureInfo.CurrentCulture, "{0} wk {1}", year, weekOfYear);
                case WeekFormats.StartOnly:
                    outputStringTemplate = "{0:d}";
                    break;
                case WeekFormats.EndOnly:
                    outputStringTemplate = "{1:d}";
                    break;
                case WeekFormats.FullDate:
                    outputStringTemplate = "{0:d} - {1:d}";
                    break;
                default:
                    break;
            }

            var yearStart = new DateTime(year, 1, 1);
            var offsetToThursday = DayOfWeek.Thursday - yearStart.DayOfWeek;

            var firstThursday = yearStart.AddDays(offsetToThursday);
            var calendar = CultureInfo.CurrentCulture.Calendar;
            var firstWeek = calendar.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            var offsetWeek = 0;
            if (firstWeek <= 1)
            {
                offsetWeek = 1;
            }

            var startOfWeek = firstThursday.AddDays((weekOfYear - offsetWeek) * 7 - 3);

            return string.Format(CultureInfo.CurrentCulture, outputStringTemplate, startOfWeek, startOfWeek.AddDays(7));
        }

        public string DisplayPercentage(float amount, float total)
        {
            return (amount / total).ToString("P0", CultureInfo.CurrentCulture);
        }

        public string DisplayShortNumber(double number) => DisplayShortNumber(number, sigFigures: 3);

        internal static string DisplayShortNumber(double number, int sigFigures = 3)
        {
            var numDiv = 0;

            while (number >= 1000)
            {
                number /= 1000;
                numDiv++;
            }

            // Find a rounding factor based on size, and round to sigFigures, e.g. for 3 sig figs, 1.776545 becomes 1.78.
            var placeValues = Math.Ceiling(Math.Log10(number));
            var roundingFactor = Math.Pow(10, sigFigures - placeValues);
            var roundedNum = Math.Round(number * roundingFactor) / roundingFactor;

            // Pad from right with zeroes to sigFigures length, so for 3 sig figs, 1.6 becomes 1.60
            var formattedNum = roundedNum.ToString("F" + sigFigures);
            var desiredLength = formattedNum.Contains('.') ? sigFigures + 1 : sigFigures;
            if (formattedNum.Length > desiredLength)
            {
                formattedNum = formattedNum.Substring(0, desiredLength);
            }

            formattedNum = formattedNum.TrimEnd('.');

            if (numDiv >= _magnitudeAbbreviations.Length)
            {
                return formattedNum + $" 10^{numDiv*3}";
            }
            
            return formattedNum + _magnitudeAbbreviations[numDiv];
        }
    }
}
