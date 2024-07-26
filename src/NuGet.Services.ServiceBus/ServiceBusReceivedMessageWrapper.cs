// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace NuGet.Services.ServiceBus
{
    public class ServiceBusReceivedMessageWrapper : IReceivedBrokeredMessage
    {
        private readonly ProcessMessageEventArgs _args;

        /// <summary>
        /// Creates a new wrapper around a received Service Bus message.
        /// </summary>
        /// <param name="args">The receiver which produced the <paramref name="message"/>. The wrapper does not own the provided receiver and therefore will not close it upon <see cref="Dispose"/>.</param>
        /// <param name="message">The received message.</param>
        public ServiceBusReceivedMessageWrapper(ProcessMessageEventArgs args)
        {
            _args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public ServiceBusReceivedMessage ServiceBusReceivedMessage => _args.Message;

        public DateTimeOffset ExpiresAtUtc => ServiceBusReceivedMessage.ExpiresAt;
        public TimeSpan TimeToLive => ServiceBusReceivedMessage.TimeToLive;
        public int DeliveryCount => ServiceBusReceivedMessage.DeliveryCount;
        public IReadOnlyDictionary<string, object> Properties => ServiceBusReceivedMessage.ApplicationProperties;
        public DateTimeOffset EnqueuedTimeUtc => ServiceBusReceivedMessage.EnqueuedTime;
        public string MessageId => ServiceBusReceivedMessage.MessageId;
        public DateTimeOffset ScheduledEnqueueTimeUtc => ServiceBusReceivedMessage.ScheduledEnqueueTime;

        public string GetBody()
        {
            return GetBody<string>();
        }

        public TStream GetBody<TStream>()
        {
            return ServiceBusClientHelper.DeserializeXmlDataContract<TStream>(ServiceBusReceivedMessage.Body);
        }

        public Stream GetRawBody()
        {
            return ServiceBusReceivedMessage.Body.ToStream();
        }

        public Task CompleteAsync()
        {
            return _args.CompleteMessageAsync(ServiceBusReceivedMessage);
        }

        public Task DeadLetterAsync()
        {
            return _args.DeadLetterMessageAsync(ServiceBusReceivedMessage);
        }

        public Task AbandonAsync()
        {
            return _args.AbandonMessageAsync(ServiceBusReceivedMessage);
        }
    }
}
