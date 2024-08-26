// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation
{
    public static class ServiceProviderExtensions
    {
        public static void ValidateMessageHandlerInitialization<TMessage>(this IServiceProvider serviceProvider)
        {
            // To detect any start-up errors, test initializing the actual message processor.
            // The message processor initialized in the main path uses a wrapper message handler, which defers initilization until a message arrives.
            // This startup test allows the app to crash prior to recieving a message which it wouldn't be able to handle properly anyways.
            // Having the app crash in this case is preferable since messages won't be dead-lettered and the job heartbeats will indicate a problem.
            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<IMessageHandler<TMessage>>>();
                logger.LogInformation("Verifying that the message handler for message type {MessageType} can be resolved.", typeof(TMessage).FullName);
                try
                {
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
                    logger.LogInformation("Successfully initialized message handler type {MessageType}.", handler.GetType().FullName);
                }
                catch
                {
                    logger.LogError("Failed to initialize message handler for message type {MessageType}. The subscription processor job cannot start. See the exception logs for more details.", typeof(TMessage).FullName);
                    throw;
                }
            }
        }
    }
}
