// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public class SubscriptionClientWrapper : ISubscriptionClient
    {
        private readonly SubscriptionClient _client;
        private readonly ILogger<SubscriptionClientWrapper> _logger;

        /// <summary>
        /// Create an instance of wrapper for <see cref="SubscriptionClient"/>. Use the managed identity authentication if the `SharedAccessKey` is not
        /// spcified in the <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">This can be a connection string with shared access key or a service bus endpoint URL string to be used with managed identities.
        /// The connection string examples:
        /// <list type="number">
        /// <item>Using connection string: "Endpoint=sb://nugetdev.servicebus.windows.net/;SharedAccessKeyName=<access key name>;SharedAccessKey=<access key>" </item>
        /// <item>Using managed identity: "sb://nugetdev.servicebus.windows.net/"</item>
        /// </list>
        /// </param>
        /// <param name="topicPath">Path of the topic name</param>
        /// <param name="name"/>Subscription name</param>
        /// <param name="logger"><see cref="ILogger"/> instance</param>
        public SubscriptionClientWrapper(string connectionString, string topicPath, string name, ILogger<SubscriptionClientWrapper> logger)
        {
            _client = connectionString.Contains(Constants.SharedAccessKeytoken)
                ? SubscriptionClient.CreateFromConnectionString(connectionString, topicPath, name)
                : SubscriptionClient.CreateWithManagedIdentity(new Uri(connectionString), topicPath, name);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnMessageAsync(Func<IBrokeredMessage, Task> onMessageAsync)
        {
            var callback = CreateOnMessageAsyncCallback(onMessageAsync);

            var onMessageOptions = new OnMessageOptions();
            onMessageOptions.ExceptionReceived += OnMessageException;

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

            var onMessageOptions = optionsWrapper.GetOptions();
            onMessageOptions.ExceptionReceived += OnMessageException;

            _client.OnMessageAsync(
                CreateOnMessageAsyncCallback(onMessageAsync),
                onMessageOptions);
        }

        private void OnMessageException(object sender, ExceptionReceivedEventArgs e)
        {
            _logger.LogError(0, e.Exception, "Unhandled exception while processing ServiceBus message");
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
