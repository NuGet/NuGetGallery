// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Messaging.Email;
using System;
using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public class EmptyMessenger : IMessageService
    {
        private readonly ILogger<EmptyMessenger> _logger;

        public EmptyMessenger(ILogger<EmptyMessenger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendMessageAsync(IEmailBuilder emailBuilder, bool copySender = false, bool discloseSenderAddress = false)
        {
            _logger.LogInformation("Message Sending with Subject: {Subject}", emailBuilder.GetSubject());
            _logger.LogInformation("Body: {Body}", emailBuilder.GetBody(EmailFormat.PlainText));
            _logger.LogInformation("Sender: {Sender}", emailBuilder.Sender.Address);
            
            _logger.LogWarning("Empty messenger used. Sending a message is no-oping.");
            return;
        }
    }
}
