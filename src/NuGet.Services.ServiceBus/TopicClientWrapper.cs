// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace NuGet.Services.ServiceBus
{
    public class TopicClientWrapper : ITopicClient
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSender _sender;

        /// <summary>
        /// Create an instance of wrapper for <see cref="TopicClient"/>. Use the managed identity authentication if the `SharedAccessKey` is not
        /// specified in the <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">This can be a connection string with shared access key or a service bus endpoint URL string to be used with managed identities.
        /// The connection string examples:
        /// <list type="number">
        /// <item>Using connection string: "Endpoint=sb://nugetdev.servicebus.windows.net/;SharedAccessKeyName=&lt;access key name&gt;SharedAccessKey=&lt;access key&gt;"</item>
        /// <item>Using managed identity: "sb://nugetdev.servicebus.windows.net/"</item>
        /// </list>
        /// </param>
        /// <param name="path">Path of the topic name</param>
        /// <param name="managedIdentityClientId">The client ID of the managed identity to try. This should be used for a user-assigned managed identity.</param>
        public TopicClientWrapper(string connectionString, string path) : this(connectionString, path, managedIdentityClientId: null)
        {
        }

        /// <summary>
        /// Create an instance of wrapper for <see cref="TopicClient"/>. Use the managed identity authentication if the `SharedAccessKey` is not
        /// specified in the <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">This can be a connection string with shared access key or a service bus endpoint URL string to be used with managed identities.
        /// The connection string examples:
        /// <list type="number">
        /// <item>Using connection string: "Endpoint=sb://nugetdev.servicebus.windows.net/;SharedAccessKeyName=&lt;access key name&gt;SharedAccessKey=&lt;access key&gt;"</item>
        /// <item>Using managed identity: "sb://nugetdev.servicebus.windows.net/"</item>
        /// </list>
        /// </param>
        /// <param name="path">Path of the topic name</param>
        /// <param name="managedIdentityClientId">The client ID of the managed identity to try. This should be used for a user-assigned managed identity.</param>
        public TopicClientWrapper(string connectionString, string path, string managedIdentityClientId)
        {
            _serviceBusClient = ServiceBusClientHelper.GetServiceBusClient(connectionString, managedIdentityClientId);
            _sender = _serviceBusClient.CreateSender(path);
        }

        public TopicClientWrapper(string clientId, string clientSecret, string tenantId, string serviceBusUrl, string path)
        {
            _serviceBusClient = new ServiceBusClient(
                serviceBusUrl ?? throw new ArgumentNullException(nameof(serviceBusUrl)),
                new ClientSecretCredential(
                    tenantId ?? throw new ArgumentNullException(nameof(tenantId)),
                    clientId ?? throw new ArgumentNullException(nameof(clientId)),
                    clientSecret ?? throw new ArgumentNullException(nameof(clientId))));
            _sender = _serviceBusClient.CreateSender(path);
        }

        public Task SendAsync(IBrokeredMessage message)
        {
            var innerMessage = GetServiceBusMessage(message);
            return _sender.SendMessageAsync(innerMessage);
        }

        public async Task CloseAsync()
        {
            await _sender.CloseAsync();
            await _serviceBusClient.DisposeAsync();
        }

        private ServiceBusMessage GetServiceBusMessage(IBrokeredMessage message)
        {
            // For now, assume the only implementation is the wrapper type. We could clone over all properties
            // that the interface supports, but this is not necessary right now.
            var wrapper = message as ServiceBusMessageWrapper;
            ServiceBusMessage innerMessage;
            if (message != null)
            {
                innerMessage = wrapper.ServiceBusMessage;
            }
            else
            {
                throw new ArgumentException(
                    $"The message must be of type {typeof(ServiceBusMessageWrapper).FullName}.",
                    nameof(message));
            }

            return innerMessage;
        }
    }
}
