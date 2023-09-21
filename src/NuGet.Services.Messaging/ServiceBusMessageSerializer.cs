// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Messaging
{
    public class ServiceBusMessageSerializer : IServiceBusMessageSerializer
    {
        private const string EmailMessageSchemaName = "EmailMessageData";

        // GDPR: EmailMessageData cannot live for more than two days because it can contain PII.
        private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromDays(2);
        private static readonly BrokeredMessageSerializer<EmailMessageData1> _serializer = new BrokeredMessageSerializer<EmailMessageData1>();

        public EmailMessageData DeserializeEmailMessageData(IReceivedBrokeredMessage message)
        {
            var data = _serializer.Deserialize(message);

            return new EmailMessageData(
                data.Subject,
                data.PlainTextBody,
                data.HtmlBody,
                data.Sender,
                data.To,
                data.CC,
                data.Bcc,
                data.ReplyTo,
                data.MessageTrackingId,
                message.DeliveryCount);
        }

        public IBrokeredMessage SerializeEmailMessageData(EmailMessageData message)
        {
            var brokeredMessage = _serializer.Serialize(new EmailMessageData1
            {
                Subject = message.Subject,
                PlainTextBody = message.PlainTextBody,
                HtmlBody = message.HtmlBody,
                Sender = message.Sender,
                To = message.To,
                CC = message.CC,
                Bcc = message.Bcc,
                ReplyTo = message.ReplyTo,
                MessageTrackingId = message.MessageTrackingId
            });

            brokeredMessage.TimeToLive = DefaultTimeToLive;

            return brokeredMessage;
        }

        [Schema(Name = EmailMessageSchemaName, Version = 1)]
        private class EmailMessageData1
        {
            public string Subject { get; set; }
            public string PlainTextBody { get; set; }
            public string HtmlBody { get; set; }
            public string Sender { get; set; }
            public Guid MessageTrackingId { get; set; }
            public IReadOnlyList<string> To { get; set; }
            public IReadOnlyList<string> CC { get; set; }
            public IReadOnlyList<string> Bcc { get; set; }
            public IReadOnlyList<string> ReplyTo { get; set; }
        }
    }
}
