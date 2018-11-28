// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Messaging.Email
{
    public class AsynchronousEmailMessageService : IMessageService
    {
        private readonly IEmailMessageEnqueuer _emailMessageEnqueuer;
        private readonly ILogger<AsynchronousEmailMessageService> _logger;
        private readonly IMessageServiceConfiguration _configuration;

        public AsynchronousEmailMessageService(
            IEmailMessageEnqueuer emailMessageEnqueuer,
            ILogger<AsynchronousEmailMessageService> logger,
            IMessageServiceConfiguration configuration)
        {
            _emailMessageEnqueuer = emailMessageEnqueuer ?? throw new ArgumentNullException(nameof(emailMessageEnqueuer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task SendMessageAsync(
            IEmailBuilder emailBuilder,
            bool copySender = false,
            bool discloseSenderAddress = false)
        {
            if (emailBuilder == null)
            {
                throw new ArgumentNullException(nameof(emailBuilder));
            }

            _logger.LogInformation("Sending message with CopySender {CopySender} and DiscloseSenderAddress {DiscloseSenderAddress}",
                copySender, discloseSenderAddress);

            var message = CreateMessage(
                emailBuilder,
                copySender,
                discloseSenderAddress);

            await EnqueueMessageAsync(message);

            // If we're asked to copy the sender but cannot disclose the sender address,
            // we should send a separate email to the sender.
            // The case where copySender=true and discloseSenderAddress=true is handled by the respective message,
            // which may add the sender to the CC recipients.
            if (copySender && !discloseSenderAddress)
            {
                await EnqueueMessageToSenderAsync(emailBuilder);
            }
        }

        private EmailMessageData CreateMessage(
            IEmailBuilder emailBuilder,
            bool copySender = false,
            bool discloseSenderAddress = false)
        {
            var recipients = emailBuilder.GetRecipients();

            if (!recipients.To.Any())
            {
                _logger.LogInformation("Cannot create message to send as it has no recipients.");
                return null;
            }

            if (emailBuilder.Sender == null)
            {
                throw new ArgumentException(
                    $"No sender defined for message of type '{emailBuilder.GetType()}'.",
                    nameof(emailBuilder.Sender));
            }

            return new EmailMessageData(
                emailBuilder.GetSubject(),
                emailBuilder.GetBody(EmailFormat.PlainText),
                emailBuilder.GetBody(EmailFormat.Html),
                emailBuilder.Sender.Address,
                to: recipients.To.Select(e => e.Address).ToList(),
                cc: GenerateCC(
                    emailBuilder.Sender.Address,
                    recipients.CC.Select(e => e.Address).ToList(),
                    copySender,
                    discloseSenderAddress),
                bcc: recipients.Bcc.Select(e => e.Address).ToList(),
                replyTo: recipients.ReplyTo.Select(e => e.Address).ToList(),
                messageTrackingId: Guid.NewGuid());
        }

        private static IReadOnlyList<string> GenerateCC(
            string fromAddress,
            IReadOnlyList<string> cc, bool copySender,
            bool discloseSenderAddress)
        {
            var ccList = new List<string>();
            if (cc != null)
            {
                ccList.AddRange(cc);
            }

            if (copySender && discloseSenderAddress && !ccList.Contains(fromAddress))
            {
                ccList.Add(fromAddress);
            }

            return ccList;
        }

        private Task EnqueueMessageAsync(EmailMessageData message)
        {
            if (message == null || !message.To.Any())
            {
                _logger.LogInformation("Skipping enqueueing message because it is null or has no recipients.");
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(message.HtmlBody)
                && string.IsNullOrEmpty(message.PlainTextBody))
            {
                throw new ArgumentException(
                    "No message body defined. Both plain-text and html bodies are empty.",
                    nameof(message));
            }

            return _emailMessageEnqueuer.SendEmailMessageAsync(message);
        }

        private Task EnqueueMessageToSenderAsync(IEmailBuilder emailBuilder)
        {
            var originalRecipients = emailBuilder.GetRecipients();
            if (!originalRecipients.To.Any())
            {
                _logger.LogInformation("Cannot create message to sender as the original message has no recipients.");
                return null;
            }

            if (emailBuilder.Sender == null)
            {
                throw new ArgumentException(
                    $"No sender defined for message of type '{emailBuilder.GetType()}'.",
                    nameof(emailBuilder.Sender));
            }

            var plainTextBody = string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        _configuration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        emailBuilder.GetBody(EmailFormat.PlainText));

            var htmlBody = string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        _configuration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        emailBuilder.GetBody(EmailFormat.Html));

            // We do not CC or BCC anyone as we do not want to disclose the sender address
            // when sending a separate message (otherwise we'd just have CC-ed the sender).
            var messageToSender = new EmailMessageData(
                emailBuilder.GetSubject() + " [Sender Copy]",
                plainTextBody,
                htmlBody,
                sender: _configuration.GalleryOwner.Address,
                to: originalRecipients.ReplyTo.Select(e => e.Address).ToList(),
                cc: null,
                bcc: null,
                replyTo: originalRecipients.ReplyTo.Select(e => e.Address).ToList(),
                messageTrackingId: Guid.NewGuid());

            return _emailMessageEnqueuer.SendEmailMessageAsync(messageToSender);
        }
    }
}