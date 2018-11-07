// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Messaging.Email;

namespace NuGet.SupportRequests.Notifications.Services
{
    internal class MessagingService
    {
        private readonly ILogger<MessagingService> _logger;
        private readonly IMessageService _messageService;

        public MessagingService(IMessageService messageService, ILogger<MessagingService> logger)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal async Task SendNotification(
            string subject,
            string htmlBody,
            DateTime referenceTime,
            string targetEmailAddress)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(nameof(subject));
            }

            if (string.IsNullOrEmpty(htmlBody))
            {
                throw new ArgumentException(nameof(htmlBody));
            }

            if (string.IsNullOrEmpty(targetEmailAddress))
            {
                throw new ArgumentException(nameof(targetEmailAddress));
            }

            var builder = new SupportRequestNotificationEmailBuilder(subject, htmlBody, targetEmailAddress);
            await _messageService.SendMessageAsync(builder);

            _logger.LogInformation(
                "Successfully sent notification '{NotificationType}' for reference time '{ReferenceTimeUtc}'",
                subject,
                referenceTime.ToShortDateString());
        }
    }
}
