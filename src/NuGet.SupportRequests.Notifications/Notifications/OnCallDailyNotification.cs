// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.SupportRequests.Notifications.Models;

namespace NuGet.SupportRequests.Notifications.Notifications
{
    internal class OnCallDailyNotification
        : INotification
    {
        public OnCallDailyNotification(
            DateTime referenceTime,
            List<SupportRequest> unresolvedIssues,
            string onCallEmailAddress)
        {
            if (referenceTime == null)
            {
                throw new ArgumentNullException(nameof(referenceTime));
            }

            if (unresolvedIssues == null)
            {
                throw new ArgumentNullException(nameof(unresolvedIssues));
            }

            if (string.IsNullOrEmpty(onCallEmailAddress))
            {
                throw new ArgumentException(nameof(onCallEmailAddress));
            }

            ReferenceTime = referenceTime;
            UnresolvedIssues = unresolvedIssues;
            TargetEmailAddress = onCallEmailAddress;
        }

        public string TemplateName => "OnCallSummary.html";

        public string Subject => "NuGet Support - On-Call Daily Summary";

        public string TargetEmailAddress { get; }

        public DateTime ReferenceTime { get; }

        public IReadOnlyCollection<SupportRequest> UnresolvedIssues { get; }
    }
}