// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public class SubscriptionClientWrapper : ISubscriptionClient
    {
        private readonly SubscriptionClient _client;

        public SubscriptionClientWrapper(string connectionString, string topicPath, string name)
        {
            _client = SubscriptionClient.CreateFromConnectionString(connectionString, topicPath, name);
        }

        public void OnMessageAsync(Func<IBrokeredMessage, Task> onMessageAsync)
        {
            _client.OnMessageAsync(innerMessage =>
            {
                var message = new BrokeredMessageWrapper(innerMessage);
                return onMessageAsync(message);
            });
        }

        public Task CloseAsync()
        {
            return _client.CloseAsync();
        }
    }
}
