// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private InitializationConfiguration _configuration;

        private readonly SupportRequestRepository _supportRequestRepository;
        private readonly MessagingService _messagingService;

        protected SupportRequestsNotificationScheduledTask(
            InitializationConfiguration configuration,
            Func<Task<SqlConnection>> openSupportRequestSqlConnectionAsync,
            ILoggerFactory loggerFactory)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _messagingService = new MessagingService(loggerFactory, configuration.SmtpUri);
            
            _supportRequestRepository = new SupportRequestRepository(loggerFactory, openSupportRequestSqlConnectionAsync);
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
