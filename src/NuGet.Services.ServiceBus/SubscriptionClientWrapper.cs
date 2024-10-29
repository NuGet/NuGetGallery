// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.ServiceBus
{
    public class SubscriptionClientWrapper : ISubscriptionClient
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly string _topicPath;
        private readonly string _name;
        private ServiceBusProcessor _processor;
        private readonly ILogger<SubscriptionClientWrapper> _logger;

        /// <summary>
        /// Create an instance of wrapper for <see cref="SubscriptionClient"/>. Use the managed identity authentication if the `SharedAccessKey` is not
        /// specified in the <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">This can be a connection string with shared access key or a service bus endpoint URL string to be used with managed identities.
        /// The connection string examples:
        /// <list type="number">
        /// <item>Using connection string: "Endpoint=sb://nugetdev.servicebus.windows.net/;SharedAccessKeyName=&lt;access key name&gt;;SharedAccessKey=&lt;access key&gt;"</item>
        /// <item>Using managed identity: "sb://nugetdev.servicebus.windows.net/"</item>
        /// </list>
        /// </param>
        /// <param name="topicPath">Path of the topic name</param>
        /// <param name="name">Subscription name</param>
        /// <param name="logger"><see cref="ILogger"/> instance</param>
        public SubscriptionClientWrapper(string connectionString, string topicPath, string name, ILogger<SubscriptionClientWrapper> logger)
            : this(connectionString, topicPath, name, managedIdentityClientId: null, logger)
        {
        }

        /// <summary>
        /// Create an instance of wrapper for <see cref="SubscriptionClient"/>. Use the managed identity authentication if the `SharedAccessKey` is not
        /// specified in the <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">This can be a connection string with shared access key or a service bus endpoint URL string to be used with managed identities.
        /// The connection string examples:
        /// <list type="number">
        /// <item>Using connection string: "Endpoint=sb://nugetdev.servicebus.windows.net/;SharedAccessKeyName=&lt;access key name&gt;;SharedAccessKey=&lt;access key&gt;"</item>
        /// <item>Using managed identity: "sb://nugetdev.servicebus.windows.net/"</item>
        /// </list>
        /// </param>
        /// <param name="topicPath">Path of the topic name</param>
        /// <param name="name">Subscription name</param>
        /// <param name="managedIdentityClientId">The client ID of the managed identity to try. This should be used for a user-assigned managed identity.</param>
        /// <param name="logger"><see cref="ILogger"/> instance</param>
        public SubscriptionClientWrapper(string connectionString, string topicPath, string name, string managedIdentityClientId, ILogger<SubscriptionClientWrapper> logger)
        {
            _serviceBusClient = ServiceBusClientHelper.GetServiceBusClient(connectionString, managedIdentityClientId);
            _topicPath = topicPath ?? throw new ArgumentNullException(nameof(topicPath));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartProcessingAsync(Func<IReceivedBrokeredMessage, Task> onMessageAsync)
        {
            await StartProcessingAsync(onMessageAsync, new OnMessageOptionsWrapper());
        }

        public async Task StartProcessingAsync(Func<IReceivedBrokeredMessage, Task> onMessageAsync, IOnMessageOptions options)
        {
            if (_processor != null)
            {
                throw new InvalidOperationException("The subscription client is already processing messages.");
            }

            if (onMessageAsync == null)
            {
                throw new ArgumentNullException(nameof(onMessageAsync));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // For now, assume the only implementation is the wrapper type.
            if (!(options is OnMessageOptionsWrapper optionsWrapper))
            {
                throw new ArgumentException(
                    $"Options must be of type {nameof(OnMessageOptionsWrapper)}",
                    nameof(options));
            }

            var processorOptions = optionsWrapper.GetOptions();

            _processor = _serviceBusClient.CreateProcessor(_topicPath, _name, processorOptions);
            _processor.ProcessMessageAsync += CreateOnMessageAsyncCallback(onMessageAsync);
            _processor.ProcessErrorAsync += OnProcessErrorAsync;
            await _processor.StartProcessingAsync();
        }

        private Task OnProcessErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Unhandled exception while processing ServiceBus message. Error source: {ErrorSource}", args.ErrorSource);
            return Task.CompletedTask;
        }

        private Func<ProcessMessageEventArgs, Task> CreateOnMessageAsyncCallback(Func<IReceivedBrokeredMessage, Task> onMessageAsync)
        {
            return args =>
            {
                var message = new ServiceBusReceivedMessageWrapper(args);
                return onMessageAsync(message);
            };
        }

        public async Task CloseAsync()
        {
            if (_processor != null)
            {
                await _processor.CloseAsync();
            }

            await _serviceBusClient.DisposeAsync();
        }
    }
}
