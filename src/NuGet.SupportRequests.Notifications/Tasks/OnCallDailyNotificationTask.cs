// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.Notifications.Models;
using NuGet.SupportRequests.Notifications.Notifications;
using NuGet.SupportRequests.Notifications.Templates;

namespace NuGet.SupportRequests.Notifications.Tasks
{
    internal class OnCallDailyNotificationTask
      : SupportRequestsNotificationScheduledTask<OnCallDailyNotification>
    {
        private const string _targetEmailAddressFormat = "{0}@microsoft.com";
        private readonly PagerDutyClient _pagerDutyClient;

        public OnCallDailyNotificationTask(
            InitializationConfiguration configuration,
            Func<Task<SqlConnection>> openSupportRequestSqlConnectionAsync,
            ILoggerFactory loggerFactory)
          : base(configuration, openSupportRequestSqlConnectionAsync, loggerFactory)
        {
            var pagerDutyConfiguration = new PagerDutyConfiguration(
                configuration.PagerDutyAccountName,
                configuration.PagerDutyApiKey
            );

            _pagerDutyClient = new PagerDutyClient(pagerDutyConfiguration);
        }

        protected override async Task<OnCallDailyNotification> BuildNotification(
            SupportRequestRepository supportRequestRepository,
            DateTime referenceTime)
        {
            var onCallAlias = await _pagerDutyClient.GetPrimaryOnCallAsync();
            var targetEmailAddress = string.Format(_targetEmailAddressFormat, onCallAlias);

            List<SupportRequest> unresolvedIssues;
            using (var connection = await supportRequestRepository.OpenConnectionAsync())
            {
                unresolvedIssues = await supportRequestRepository.GetUnresolvedIssues(connection);
            }

            return new OnCallDailyNotification(referenceTime, unresolvedIssues, targetEmailAddress);
        }

        protected override string BuildNotificationHtmlBody(string template, OnCallDailyNotification notification)
        {
            var result = template;
            var referenceTimeLabel = notification.ReferenceTime.ToString("dd/MM/yy");

            if (notification.UnresolvedIssues.Any())
            {
                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.New).ToList(),
                    result,
                    HtmlPlaceholders.NewIssues,
                    HtmlSnippets.NoNewIssuesReportedOn(referenceTimeLabel));

                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.Working).ToList(),
                    result,
                    HtmlPlaceholders.WorkingIssues,
                    HtmlSnippets.NoWorkingIssuesOn(referenceTimeLabel));

                result = InjectIssueStatus(
                    notification.UnresolvedIssues.Where(i => i.IssueStatus == (int)IssueStatusKeys.WaitingForCustomer).ToList(),
                    result,
                    HtmlPlaceholders.WaitingForCustomerIssues,
                    HtmlSnippets.NoIssuesWaitingForCustomerOn(referenceTimeLabel));
            }
            else
            {
                result = result
                    .Replace(HtmlPlaceholders.NewIssues, HtmlSnippets.NoNewIssuesReportedOn(referenceTimeLabel))
                    .Replace(HtmlPlaceholders.WorkingIssues, HtmlSnippets.NoWorkingIssuesOn(referenceTimeLabel))
                    .Replace(HtmlPlaceholders.WaitingForCustomerIssues, HtmlSnippets.NoIssuesWaitingForCustomerOn(referenceTimeLabel));
            }

            result = result.Replace(HtmlPlaceholders.ReportDate, referenceTimeLabel);

            return result;
        }

        private static string InjectIssueStatus(
            IReadOnlyCollection<SupportRequest> issues,
            string result,
            string issuePlaceholder,
            string noIssuesHtmlSnippet)
        {
            if (issuePlaceholder == null)
            {
                throw new ArgumentNullException(nameof(issuePlaceholder));
            }

            if (!issues.Any())
            {
                result = result.Replace(issuePlaceholder, noIssuesHtmlSnippet);
            }

            var newIssuesStringBuilder = new StringBuilder();
            foreach (var issuesByCreator in issues
                .OrderBy(i => i.CreatedDate)
                .GroupBy(i => i.CreatedBy)
                .OrderBy(i => i.Key))
            {
                foreach (var supportRequest in issuesByCreator)
                {
                    var issueHtml = BuildIssueHtml(issuesByCreator.Key, supportRequest);

                    newIssuesStringBuilder.Append(issueHtml);
                }
            }

            result = result.Replace(
                issuePlaceholder,
                newIssuesStringBuilder.ToString());
            return result;
        }

        private static string BuildIssueHtml(string createdByUserName, SupportRequest supportRequest)
        {
            var issueCreatorProfileLink = CreateUserProfileLink(createdByUserName);

            var issueHtmlTemplate = HtmlSnippets.IssueTemplate
                .Replace(HtmlPlaceholders.From,
                    $"[{supportRequest.CreatedDate:MM/dd hh:mm tt} UTC] - {issueCreatorProfileLink}");

            var issueHtml = issueHtmlTemplate.Replace(HtmlPlaceholders.Reason, supportRequest.Reason);

            if (!string.IsNullOrEmpty(supportRequest.PackageId))
            {
                var packageLinkHtml = HtmlSnippets.PackageLinkTemplate
                    .Replace(HtmlPlaceholders.PackageId, supportRequest.PackageId);

                if (!string.IsNullOrEmpty(supportRequest.PackageVersion))
                {
                    packageLinkHtml = packageLinkHtml
                        .Replace(HtmlPlaceholders.PackageVersion, supportRequest.PackageVersion)
                        .Replace(HtmlPlaceholders.PackageVersionLabel, $" v{supportRequest.PackageVersion}");
                }
                else
                {
                    packageLinkHtml = packageLinkHtml
                        .Replace(HtmlPlaceholders.PackageVersion, string.Empty)
                        .Replace(HtmlPlaceholders.PackageVersionLabel, string.Empty);
                }

                issueHtml = issueHtml
                    .Replace(HtmlPlaceholders.PackageLink, packageLinkHtml);
            }
            else
            {
                issueHtml = issueHtml.Replace(HtmlPlaceholders.PackageLink, HtmlSnippets.EmptyResultSpan);
            }
            return issueHtml;
        }

        private static string CreateUserProfileLink(string userName)
        {
            var issueCreatorProfileLink =
                string.Equals("anonymous", userName, StringComparison.OrdinalIgnoreCase)
                    ? userName
                    : string.Format(HtmlSnippets.IssueCreatorTemplate, userName);

            return issueCreatorProfileLink;
        }
    }
}
