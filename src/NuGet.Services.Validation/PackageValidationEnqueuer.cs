// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public class PackageValidationEnqueuer : IPackageValidationEnqueuer
    {
        private readonly ITopicClient _topicClient;
        private readonly IServiceBusMessageSerializer _serializer;

        public PackageValidationEnqueuer(ITopicClient topicClient, IServiceBusMessageSerializer serializer)
        {
            _topicClient = topicClient;
            _serializer = serializer;
        }

        public async Task SendMessageAsync(PackageValidationMessageData message)
        {
            await SendMessageAsync(message, DateTimeOffset.MinValue);
        }

        public async Task SendMessageAsync(PackageValidationMessageData message, DateTimeOffset postponeProcessingTill)
        {
            var brokeredMessage = _serializer.SerializePackageValidationMessageData(message);
            brokeredMessage.ScheduledEnqueueTimeUtc = postponeProcessingTill;
            await _topicClient.SendAsync(brokeredMessage);
        }
    }
}
