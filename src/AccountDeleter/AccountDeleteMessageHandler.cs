// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery.AccountDeleter.Messengers;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteMessageHandler : IMessageHandler<AccountDeleteMessage>
    {
        private readonly IAccountManager _accountManager;
        private readonly IMessageService _messenger;
        private readonly IEmailBuilderFactory _emailBuilderFactory;
        private readonly IAccountDeleteTelemetryService _telemetryService;
        private readonly ILogger<AccountDeleteMessageHandler> _logger;

        public AccountDeleteMessageHandler (
            IAccountManager accountManager,
            IMessageService messenger,
            IEmailBuilderFactory emailBuilderFactory,
            IAccountDeleteTelemetryService telemetryService,
            ILogger<AccountDeleteMessageHandler> logger)
        {
            _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _emailBuilderFactory = emailBuilderFactory ?? throw new ArgumentNullException(nameof(emailBuilderFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(AccountDeleteMessage command)
        {
            var messageProcessed = true;
            var username = command.Subject;
            _logger.LogInformation("Processing Request from Source {Source}", command.Source);

            var source = command.Source;
            var deleteSuccess = await _accountManager.DeleteAccount(username);

            try
            {
                var baseEmailBuilder = _emailBuilderFactory.GetEmailBuilder(source, deleteSuccess);
                if (baseEmailBuilder != null)
                {
                    var recipientEmail = await _accountManager.GetEmailAddresForUser(username);

                    var toEmail = new List<MailAddress>();
                    toEmail.Add(new MailAddress(recipientEmail));

                    var recipients = new EmailRecipients(toEmail);
                    var emailBuilder = new DisposableEmailBuilder(baseEmailBuilder, recipients);
                    await _messenger.SendMessageAsync(emailBuilder, copySender: true);
                }
            }
            catch (UnknownSourceException)
            {
                // Should definitely log if source isn't expected. Should we even send mail? or log and alert?
                // Log unknown source and fail.
                _logger.LogError("Unknown message source detected: {Source}.", command.Source);
                messageProcessed = false;
            }
            catch (EmailContactNotAllowedException)
            {
                // Should we not send? or should we ignore the setting.
                _logger.LogWarning("User did not allow Email Contact.");
            }
            catch (Exception e)
            {
                _logger.LogError("An unknown exception occured: {ExceptionMessage}", e.Message);
                throw e;
            }

            return messageProcessed;
        }
    }
}
