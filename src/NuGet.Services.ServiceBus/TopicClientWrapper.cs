// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace NuGet.Services.ServiceBus
{
    public class TopicClientWrapper : ITopicClient
    {
        private readonly TopicClient _client;

        public TopicClientWrapper(string connectionString, string path)
        {
            _client = TopicClient.CreateFromConnectionString(connectionString, path);
        }

        public TopicClientWrapper(string clientId, string clientSecret, string tenantId, string serviceBusUrl, string path)
        {
            AzureActiveDirectoryTokenProvider.AuthenticationCallback authCallback = async (audience, authority, state) =>
            {
                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithAuthority(authority)
                    .WithClientSecret(clientSecret)
                    .Build();

                var result = await app
                    .AcquireTokenForClient(new string[] { "https://servicebus.azure.net/.default" })
                    .ExecuteAsync();

                return result.AccessToken;
            };

            _client = TopicClient.CreateWithAzureActiveDirectory(new Uri(serviceBusUrl), path, authCallback, $"https://login.windows.net/{tenantId}");
        }

        public Task SendAsync(IBrokeredMessage message)
        {
            var innerMessage = GetBrokeredMessage(message);
            return _client.SendAsync(innerMessage);
        }

        public void Send(IBrokeredMessage message)
        {
            var innerMessage = GetBrokeredMessage(message);
            _client.Send(innerMessage);
        }

        public void Close()
        {
            _client.Close();
        }

        public async Task CloseAsync()
        {
            await _client.CloseAsync();
        }

        private BrokeredMessage GetBrokeredMessage(IBrokeredMessage message)
        {
            // For now, assume the only implementation is the wrapper type. We could clone over all properties
            // that the interface supports, but this is not necessary right now.
            var wrapper = message as BrokeredMessageWrapper;
            BrokeredMessage innerMessage;
            if (message != null)
            {
                innerMessage = wrapper.BrokeredMessage;
            }
            else
            {
                throw new ArgumentException(
                    $"The message must be of type {typeof(BrokeredMessageWrapper).FullName}.",
                    nameof(message));
            }

            return innerMessage;
        }
    }
}
