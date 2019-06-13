// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteMessageHandler : IMessageHandler<AccountDeleteMessage>
    {
        private IAccountManager _accountManager;
        private IMessenger _messenger;
        private readonly IAccountDeleteTelemetryService _telemetryService;
        private readonly ILogger<AccountDeleteMessageHandler> _logger;

        public AccountDeleteMessageHandler (
            IAccountManager accountManager,
            IMessenger messenger,
            IAccountDeleteTelemetryService telemetryService,
            ILogger<AccountDeleteMessageHandler> logger)
        {
            _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(AccountDeleteMessage command)
        {
            bool messageProcessed = false;
            var username = command.Subject;
            _logger.LogInformation("Processing Request from Source {Source}", command.Source);

            if (!await _accountManager.DeleteAccount(username))
            {
                // switch on command source here?
                switch (command.Source)
                {
                    case "Gallery":
                        break;
                    case "DSR":
                        messageProcessed = messageProcessed || await _messenger.SendMessageAsync(username, 0); // This will probalby need to be expanded to flag the source somehow if we want to send varying messages. Who should be responsible for formatting?
                        break;
                    case "GalleryAdmin":
                        break;
                    default:
                        // Should definitely log if source isn't expected. Should we even send mail? or log and alert?
                        // Log unknown source and fail.
                        _logger.LogError("Unknown message source detected: {Source}", command.Source);
                        break;
                }
            }

            return messageProcessed;
        }
    }
}
