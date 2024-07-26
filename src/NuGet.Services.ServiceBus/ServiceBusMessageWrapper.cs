// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace NuGet.Services.ServiceBus
{
    public class ServiceBusMessageWrapper : IBrokeredMessage
    {
        public ServiceBusMessageWrapper(string data)
        {
            // Default to a random message ID. This is a behavior from the legacy Service Bus SDK. A message ID is
            // required on a topic with partitioning enabled.
            ServiceBusMessage = new ServiceBusMessage(ServiceBusClientHelper.SerializeXmlDataContract(data))
            {
                MessageId = Guid.NewGuid().ToString("N"),
            };
        }

        public ServiceBusMessageWrapper(ServiceBusMessage brokeredMessage)
        {
            ServiceBusMessage = brokeredMessage ?? throw new ArgumentNullException(nameof(brokeredMessage));
        }

        public ServiceBusMessage ServiceBusMessage { get; }

        public TimeSpan TimeToLive
        {
            get => ServiceBusMessage.TimeToLive;
            set => ServiceBusMessage.TimeToLive = value;
        }

        public IDictionary<string, object> Properties => ServiceBusMessage.ApplicationProperties;

        public string MessageId
        {
            get => ServiceBusMessage.MessageId;
            set => ServiceBusMessage.MessageId = value;
        }

        public DateTimeOffset ScheduledEnqueueTimeUtc
        {
            get => ServiceBusMessage.ScheduledEnqueueTime;
            set => ServiceBusMessage.ScheduledEnqueueTime = value;
        }

        public string GetBody()
        {
            return GetBody<string>();
        }

        public TStream GetBody<TStream>()
        {
            return ServiceBusClientHelper.DeserializeXmlDataContract<TStream>(ServiceBusMessage.Body);
        }
    }
}
