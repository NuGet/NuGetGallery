// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.SupportRequests.Notifications.Models
{
    internal class SingleWeekSummary
    {
        public SingleWeekSummary(
            IReadOnlyCollection<SupportRequest> unresolvedIssues,
            int closedCount,
            int createdCount,
            TimeSpan averageTimeToResolution)
        {
            ClosedCount = closedCount;
            CreatedCount = createdCount;
            AverageTimeToResolution = averageTimeToResolution;
            UnresolvedCount = unresolvedIssues.Count;

            WaitingForCustomerCount = unresolvedIssues.Count(i =>
                i.IssueStatus == (int) IssueStatusKeys.WaitingForCustomer);

            InProgressCount = unresolvedIssues.Count(i =>
                i.IssueStatus == (int) IssueStatusKeys.Working);
        }

        public int CreatedCount { get; }
        public TimeSpan AverageTimeToResolution { get; }
        public int ClosedCount { get; }
        public int InProgressCount { get; }
        public int UnresolvedCount { get; }
        public int WaitingForCustomerCount { get; }

        public double GetUnresolvedPercentage()
        {
            if (CreatedCount == 0)
            {
                return UnresolvedCount;
            }

            return (double)UnresolvedCount / CreatedCount;
        }
    }
}