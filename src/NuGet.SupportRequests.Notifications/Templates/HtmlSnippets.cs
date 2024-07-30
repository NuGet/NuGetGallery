// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.Notifications.Templates
{
    internal static class HtmlSnippets
    {
        private const string _positiveInfinityLabel = "+ ∞";
        private const string _negativeInfinityLabel = "- ∞";

        private static readonly string _trendImageTemplate =
            $"<span class=\"arr\"><img alt=\"\" src=\"{HtmlPlaceholders.TrendImage}\" width=\"16\"></span> &nbsp; ";
        private static readonly string _noChangesSpan = $"{EmptyResultSpan} &nbsp; <a href=\"\">no changes</a>";

        internal const string IssueCreatorTemplate = "<a href=\"https://www.nuget.org/profiles/{0}\">{0}</a>";
        internal const string EmptyResultSpan = "<span style=\"color: #7e338c\">─</span>";


        internal static readonly string PackageLinkTemplate =
            $"<a href=\"https://www.nuget.org/packages/{HtmlPlaceholders.PackageId}/{HtmlPlaceholders.PackageVersion}\">{HtmlPlaceholders.PackageId}{HtmlPlaceholders.PackageVersionLabel}</a>";

        internal static readonly string IssueTemplate =
            $"<tr class=\"border-trim\"><th class=\"left\" align=\"left\">{HtmlPlaceholders.From}</th><td style=\"text-align:left;\">{HtmlPlaceholders.Reason}</td><td colspan=\"2\" style=\"text-align:left;\">{HtmlPlaceholders.PackageLink}</td></tr>";

        internal const string OnCallStatusTemplate =
                "<tr class=\"border-trim\"><th class=\"left\" align=\"left\" valign=\"top\" style=\"text-align: right;\">{0}</th><td valign=\"top\" style=\"text-align: left;\" colspan=\"3\">{1}</td></tr>";
        internal const string OnCallStatusItemTemplate = "<span style=\"font-weight:bold;\">{0}</span> issues <span style=\"font-weight:bold;\">{1}</span>";
        internal const string OnCallStatusItemTemplateSingle = "<span style=\"font-weight:bold;\">{0}</span> issue <span style=\"font-weight:bold;\">{1}</span>";
        internal const string OnCallStatusItemSeparator = "<br/>";

        internal static string GetTrendImage(double percentage, string upTrendImage, string downTrendImage)
        {
            if (double.IsPositiveInfinity(percentage))
            {
                return _trendImageTemplate.Replace(HtmlPlaceholders.TrendImage, upTrendImage);
            }
            else if (double.IsNegativeInfinity(percentage))
            {
                return _trendImageTemplate.Replace(HtmlPlaceholders.TrendImage, downTrendImage);
            }
            else if (double.IsNaN(percentage))
            {
                return string.Empty;
            }
            else if (percentage > 0)
            {
                return _trendImageTemplate.Replace(HtmlPlaceholders.TrendImage, upTrendImage);
            }
            else if (percentage < 0)
            {
                return _trendImageTemplate.Replace(HtmlPlaceholders.TrendImage, downTrendImage);
            }

            return string.Empty;
        }

        internal static string GetTrendPercentageString(double percentage)
        {
            if (double.IsPositiveInfinity(percentage))
            {
                return _positiveInfinityLabel;
            }
            else if (double.IsNegativeInfinity(percentage))
            {
                return _negativeInfinityLabel;
            }
            else if (!double.IsNaN(percentage) && (percentage > 0 || percentage < 0))
            {
                return percentage.ToString("P0");
            }

            return _noChangesSpan;
        }

        public static string NoNewIssuesReportedOn(string referenceTimeLabel)
        {
            return $"<tr class=\"border-trim\"><td colspan=\"4\">No new issues reported on {referenceTimeLabel}</td></tr>";
        }

        public static string NoWorkingIssuesOn(string referenceTimeLabel)
        {
            return $"<tr class=\"border-trim\"><td colspan=\"4\">No issues in progress on {referenceTimeLabel}</td></tr>";
        }

        public static string NoIssuesWaitingForCustomerOn(string referenceTimeLabel)
        {
            return $"<tr class=\"border-trim\"><td colspan=\"4\">No issues waiting for customer on {referenceTimeLabel}</td></tr>";
        }
    }
}