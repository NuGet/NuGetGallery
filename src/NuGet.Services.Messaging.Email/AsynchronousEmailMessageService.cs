// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Messaging.Email
{
    public class AsynchronousEmailMessageService : IMessageService
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly IEmailMessageEnqueuer _emailMessageEnqueuer;

        public AsynchronousEmailMessageService(
            IMessageServiceConfiguration configuration,
            IEmailMessageEnqueuer emailMessageEnqueuer)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailMessageEnqueuer = emailMessageEnqueuer ?? throw new ArgumentNullException(nameof(emailMessageEnqueuer));
        }

        public Task SendMessageAsync(
            IEmailBuilder emailBuilder,
            bool copySender = false,
            bool discloseSenderAddress = false)
        {
            if (emailBuilder == null)
            {
                throw new ArgumentNullException(nameof(emailBuilder));
            }

            var message = CreateMessage(
                emailBuilder,
                copySender,
                discloseSenderAddress);

            return EnqueueMessageAsync(message);
        }

        private static EmailMessageData CreateMessage(
            IEmailBuilder emailBuilder,
            bool copySender = false,
            bool discloseSenderAddress = false)
        {
            var recipients = emailBuilder.GetRecipients();

            if (recipients == EmailRecipients.None)
            {
                // Optimization: no need to construct message body when no recipients.
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
    }
}