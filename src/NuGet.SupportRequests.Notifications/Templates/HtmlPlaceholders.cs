// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.Notifications.Templates
{
    internal static class HtmlPlaceholders
    {
        // common
        internal const string CssStyles = "/*$$STYLE$$*/";
        internal const string ReportDate = "$$REPORT_DATE$$";
        internal const string TrendImage = "$$IMG_PLACEHOLDER$$";

        // on-call summary
        internal const string From = "$$FROM$$";
        internal const string Reason = "$$REASON$$";
        internal const string PackageId = "$$PACKAGE_ID$$";
        internal const string PackageVersion = "$$PACKAGE_VERSION$$";
        internal const string PackageVersionLabel = "$$PACKAGE_VERSION_LABEL$$";
        internal const string PackageLink = "$$PACKAGE_LINK$$";
        internal const string NewIssues = "$$NEWISSUES$$";
        internal const string WorkingIssues = "$$WORKINGISSUES$$";
        internal const string WaitingForCustomerIssues = "$$WAITINGFORCUSTOMERISSUES$$";
        internal const string OnCallStatusReport = "$$ONCALL_STATUSREPORT$$";

        // weekly summary
        internal const string NewIssueCountPriorWeek = "$$INFO_2W-AGO_NEWREQUESTS$$";
        internal const string NewIssueCountLastWeek = "$$INFO_1W-AGO_NEWREQUESTS$$";
        internal const string NewIssueCountDeltaValue = "$$INFO_1W-AGO_NEWREQUESTS_DELTAVALUE$$";
        internal const string NewIssueCountDeltaImage = "$$INFO_1W-AGO_NEWREQUESTS__DELTAIMG$$";
        internal const string ClosedIssueCountPriorWeek = "$$INFO_2W-AGO_CLOSEDREQUESTS$$";
        internal const string ClosedIssueCountLastWeek = "$$INFO_1W-AGO_CLOSEDREQUESTS$$";
        internal const string ClosedIssueCountDeltaValue = "$$INFO_1W-AGO_CLOSEDREQUESTS_DELTAVALUE$$";
        internal const string ClosedIssueCountDeltaImage = "$$INFO_1W-AGO_CLOSEDREQUESTS__DELTAIMG$$";
        internal const string UnresolvedIssueCountPriorWeek = "$$INFO_2W-AGO_UNRESOLVEDREQUESTS$$";
        internal const string UnresolvedIssueCountLastWeek = "$$INFO_1W-AGO_UNRESOLVEDREQUESTS$$";
        internal const string UnresolvedIssueCountDeltaValue = "$$INFO_1W-AGO_UNRESOLVEDREQUESTS_DELTAVALUE$$";
        internal const string UnresolvedIssueCountDeltaImage = "$$INFO_1W-AGO_UNRESOLVEDREQUESTS__DELTAIMG$$";
        internal const string WaitingForCustomerIssueCountPriorWeek = "$$INFO_2W-AGO_WAITINGREQUESTS$$";
        internal const string WaitingForCustomerIssueCountLastWeek = "$$INFO_1W-AGO_WAITINGREQUESTS$$";
        internal const string WaitingForCustomerIssueCountDeltaValue = "$$INFO_1W-AGO_WAITINGREQUESTS_DELTAVALUE$$";
        internal const string WaitingForCustomerIssueCountDeltaImage = "$$INFO_1W-AGO_WAITINGREQUESTS__DELTAIMG$$";
        internal const string InProgressIssueCountPriorWeek = "$$INFO_2W-AGO_WORKINGREQUESTS$$";
        internal const string InProgressIssueCountLastWeek = "$$INFO_1W-AGO_WORKINGREQUESTS$$";
        internal const string InProgressIssueCountDeltaValue = "$$INFO_1W-AGO_WORKINGREQUESTS_DELTAVALUE$$";
        internal const string InProgressIssueCountDeltaImage = "$$INFO_1W-AGO_WORKINGREQUESTS__DELTAIMG$$";
        internal const string UnresolvedPercentagePriorWeek = "$$INFO_2W-AGO_SLA_UNRESOLVEDPCT$$";
        internal const string UnresolvedPercentageLastWeek = "$$INFO_1W-AGO_SLA_UNRESOLVEDPCT$$";
        internal const string UnresolvedPercentageDeltaImage = "$$INFO_SLA_UNRESOLVEDPCT_DELTAIMG$$";
        internal const string UnresolvedPercentageDeltaValue = "$$INFO_SLA_UNRESOLVEDPCT_DELTAVALUE$$";
        internal const string AvgTimeToResolutionPriorWeek = "$$AVG_TIMETORESOLUTION_PRIORWEEK$$";
        internal const string AvgTimeToResolutionLastWeek = "$$AVG_TIMETORESOLUTION_LASTWEEK$$";
        internal const string AvgTimeToResolutionDeltaImage = "$$AVG_TIMETORESOLUTION_DELTAIMG$$";
        internal const string AvgTimeToResolutionDeltaValue = "$$AVG_TIMETORESOLUTION_DELTAVALUE$$";
        internal const string TopReasonPosition1Placeholder = "$$INFO_1W-AGO_TOP_REASON_1$$";
        internal const string TopReasonCountPosition1Placeholder = "$$INFO_1W-AGO_TOP_REASON_1_COUNT$$";
        internal const string TopReasonPosition2Placeholder = "$$INFO_1W-AGO_TOP_REASON_2$$";
        internal const string TopReasonCountPosition2Placeholder = "$$INFO_1W-AGO_TOP_REASON_2_COUNT$$";
        internal const string TopReasonPosition3Placeholder = "$$INFO_1W-AGO_TOP_REASON_3$$";
        internal const string TopReasonCountPosition3Placeholder = "$$INFO_1W-AGO_TOP_REASON_3_COUNT$$";
    }
}