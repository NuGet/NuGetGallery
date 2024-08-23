// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
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

            var serializer = new ServiceBusMessageSerializer();
            var topicClient = new TopicClientWrapper(configuration.EmailPublisherConnectionString, configuration.EmailPublisherTopicName);
            var enqueuer = new EmailMessageEnqueuer(topicClient, serializer, loggerFactory.CreateLogger<EmailMessageEnqueuer>());
            var messageService = new AsynchronousEmailMessageService(
                enqueuer,
                loggerFactory.CreateLogger<AsynchronousEmailMessageService>(),
                configuration);
            _messagingService = new MessagingService(messageService, loggerFactory.CreateLogger<MessagingService>());
            
            _supportRequestRepository = new SupportRequestRepository(loggerFactory, openSupportRequestSqlConnectionAsync);
        }

        protected abstract Task<TNotification> BuildNotification(SupportRequestRepository supportRequestRepository, DateTime referenceTime);

        protected abstract string BuildNotificationHtmlBody(string template, TNotification notification);

        public async Task RunAsync()
        {
            var referenceTime = DateTime.UtcNow.Date;
            var notification = await BuildNotification(_supportRequestRepository, referenceTime);
            var template = NotificationTemplateProvider.Get(notification.TemplateName);
            var htmlBody = BuildNotificationHtmlBody(template, notification);

            await _messagingService.SendNotification(
                notification.Subject,
                htmlBody,
                referenceTime,
                notification.TargetEmailAddress);
        }
    }
}
