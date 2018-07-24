// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.SupportRequests.Notifications.Notifications;
using NuGet.SupportRequests.Notifications.Services;
using NuGet.SupportRequests.Notifications.Templates;

namespace NuGet.SupportRequests.Notifications.Tasks
{
    internal abstract class SupportRequestsNotificationScheduledTask<TNotification>
      : IScheduledTask
        where TNotification : INotification
    {
        private readonly SupportRequestRepository _supportRequestRepository;
        private readonly MessagingService _messagingService;

        protected SupportRequestsNotificationScheduledTask(
            IServiceContainer serviceContainer,
            IDictionary<string, string> jobArgsDictionary,
            Func<Task<SqlConnection>> openSupportSqlConnectionAsync,
            ILoggerFactory loggerFactory)
        {
            if (jobArgsDictionary == null)
            {
                throw new ArgumentNullException(nameof(jobArgsDictionary));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            var smtpUri = jobArgsDictionary[JobArgumentNames.SmtpUri];
            _messagingService = new MessagingService(loggerFactory, smtpUri);
            
            _supportRequestRepository = new SupportRequestRepository(openSupportSqlConnectionAsync, loggerFactory);
        }

        protected abstract Task<TNotification> BuildNotification(SupportRequestRepository supportRequestRepository, DateTime referenceTime);

        protected abstract string BuildNotificationBody(string template, TNotification notification);

        public async Task RunAsync()
        {
            var referenceTime = DateTime.UtcNow.Date;
            var notification = await BuildNotification(_supportRequestRepository, referenceTime);
            var template = NotificationTemplateProvider.Get(notification.TemplateName);
            var body = BuildNotificationBody(template, notification);

            _messagingService.SendNotification(
                notification.Subject,
                body,
                referenceTime,
                notification.TargetEmailAddress);
        }
    }
}
