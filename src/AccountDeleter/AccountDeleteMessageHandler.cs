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
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<AccountDeleteMessageHandler> _logger;

        public AccountDeleteMessageHandler (
            IAccountManager accountManager,
            IMessenger messenger,
            ITelemetryService telemetryService,
            ILogger<AccountDeleteMessageHandler> logger)
        {
            _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> HandleAsync(AccountDeleteMessage command)
        {
            var username = command.Subject;

            if (!_accountManager.DeleteAccount(username))
            {
                switch (command.Source)
                {
                    case "Gallery":
                        break;
                    case "DSR":
                        break;
                    case "GalleryAdmin":
                        break;
                    default:
                        // Log unknown source and fail.
                        break;
                }

                // switch on command source here?
                // Should definitely log if source isn't expected. Should we even send mail? or log and alert?
                _messenger.SendMessageAsync(username, 0); // This will probalby need to be expanded to flag the source somehow if we want to send varying messages. Who should be responsible for formatting?
            }

            return Task.FromResult(true);
        }
    }
}
