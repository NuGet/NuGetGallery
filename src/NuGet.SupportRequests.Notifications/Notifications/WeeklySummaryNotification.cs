// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.SupportRequests.Notifications.Models;

namespace NuGet.SupportRequests.Notifications.Notifications
{
    internal class WeeklySummaryNotification
        : INotification
    {
        public WeeklySummaryNotification(
            DateTime referenceTime,
            List<SupportRequest> unresolvedIssues,
            string targetEmailAddress,
            SingleWeekSummary lastWeekSummary,
            SingleWeekSummary priorWeekSummary,
            IDictionary<string, int> topSupportRequestReasonsLastWeek)
        {
            if (referenceTime == null)
            {
                throw new ArgumentNullException(nameof(referenceTime));
            }

            if (unresolvedIssues == null)
            {
                throw new ArgumentNullException(nameof(unresolvedIssues));
            }

            if (string.IsNullOrEmpty(targetEmailAddress))
            {
                throw new ArgumentException(nameof(targetEmailAddress));
            }

            if (lastWeekSummary == null)
            {
                throw new ArgumentNullException(nameof(lastWeekSummary));
            }

            if (priorWeekSummary == null)
            {
                throw new ArgumentNullException(nameof(priorWeekSummary));
            }

            if (topSupportRequestReasonsLastWeek == null)
            {
                throw new ArgumentNullException(nameof(topSupportRequestReasonsLastWeek));
            }

            ReferenceTime = referenceTime;
            TargetEmailAddress = targetEmailAddress;
            UnresolvedIssues = unresolvedIssues;
            LastWeek = lastWeekSummary;
            PriorWeek = priorWeekSummary;
            TopSupportRequestReasonsLastWeek = topSupportRequestReasonsLastWeek;

            UnresolvedPercentageDelta = LastWeek.GetUnresolvedPercentage() -
                                        PriorWeek.GetUnresolvedPercentage();
        }

        public string TemplateName => "WeeklySummary.html";

        public string Subject => "NuGet Support - Weekly Summary";

        public DateTime ReferenceTime { get; }

        public string TargetEmailAddress { get; }

        public SingleWeekSummary LastWeek { get; }
        public SingleWeekSummary PriorWeek { get; }

        public List<SupportRequest> UnresolvedIssues { get; }

        public double GetIssuesClosedTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.ClosedCount, PriorWeek.ClosedCount);
        }

        public double GetNewIssuesTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.CreatedCount, PriorWeek.CreatedCount);
        }

        public double GetIssuesUnresolvedTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.UnresolvedCount, PriorWeek.UnresolvedCount);
        }
        public double UnresolvedPercentageDelta { get; }

        public double GetIssuesWaitingForCustomerTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.WaitingForCustomerCount, PriorWeek.WaitingForCustomerCount);
        }

        public double GetIssuesInProgressTrendPct()
        {
            return GetDeltaPercentageSafe(LastWeek.InProgressCount, PriorWeek.InProgressCount);
        }

        public IDictionary<string, int> TopSupportRequestReasonsLastWeek { get; }

        private static double GetDeltaPercentageSafe(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                if (numerator > 0)
                {
                    return double.PositiveInfinity;
                }
                else if (numerator < 0)
                {
                    return double.NegativeInfinity;
                }

                return double.NaN;
            }
            else
            {
                return (double)(numerator - denominator) / denominator;
            }
        }
    }
}
