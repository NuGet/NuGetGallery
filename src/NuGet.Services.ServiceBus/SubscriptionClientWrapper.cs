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
            var callback = CreateOnMessageAsyncCallback(onMessageAsync);

            _client.OnMessageAsync(callback);
        }

        public void OnMessageAsync(Func<IBrokeredMessage, Task> onMessageAsync, IOnMessageOptions options)
        {
            if (onMessageAsync == null) throw new ArgumentNullException(nameof(onMessageAsync));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // For now, assume the only implementation is the wrapper type.
            if (! (options is OnMessageOptionsWrapper optionsWrapper))
            {
                throw new ArgumentException(
                    $"Options must be of type {nameof(OnMessageOptionsWrapper)}",
                    nameof(options));
            }

            _client.OnMessageAsync(
                CreateOnMessageAsyncCallback(onMessageAsync),
                optionsWrapper.OnMessageOptions);
        }

        private Func<BrokeredMessage, Task> CreateOnMessageAsyncCallback(Func<IBrokeredMessage, Task> onMessageAsync)
        {
            return innerMessage =>
            {
                var message = new BrokeredMessageWrapper(innerMessage);

                return onMessageAsync(message);
            };
        }

        public Task CloseAsync()
        {
            return _client.CloseAsync();
        }
    }
}
