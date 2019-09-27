// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteMessageHandler : IMessageHandler<AccountDeleteMessage>
    {
        private readonly IOptionsSnapshot<AccountDeleteConfiguration> _accountDeleteConfigurationAccessor;
        private readonly IAccountManager _accountManager;
        private readonly IUserService _userService;
        private readonly IMessageService _messenger;
        private readonly IEmailBuilderFactory _emailBuilderFactory;
        private readonly IAccountDeleteTelemetryService _telemetryService;
        private readonly ILogger<AccountDeleteMessageHandler> _logger;

        public AccountDeleteMessageHandler(
            IOptionsSnapshot<AccountDeleteConfiguration> accountDeleteConfigurationAccessor,
            IAccountManager accountManager,
            IUserService userService,
            IMessageService messenger,
            IEmailBuilderFactory emailBuilderFactory,
            IAccountDeleteTelemetryService telemetryService,
            ILogger<AccountDeleteMessageHandler> logger)
        {
            _accountDeleteConfigurationAccessor = accountDeleteConfigurationAccessor ?? throw new ArgumentNullException(nameof(accountDeleteConfigurationAccessor));
            _accountManager = accountManager ?? throw new ArgumentNullException(nameof(accountManager));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _emailBuilderFactory = emailBuilderFactory ?? throw new ArgumentNullException(nameof(emailBuilderFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles incoming messages of AccountDeleteMessage type
        /// </summary>
        /// <param name="command"></param>
        /// <returns>True if processed successfully, false if we failed for some reason.</returns>
        /// <remarks>If accountManager.DeleteAccount throws, the entire method will throw and the message will go back into the queue.</remarks>
        public async Task<bool> HandleAsync(AccountDeleteMessage command)
        {
            var messageProcessed = true;
            var source = command.Source;

            try
            {
                _logger.LogInformation("Processing Request from Source {Source}", source);
                _accountDeleteConfigurationAccessor.Value.GetSourceConfiguration(source);

                var username = command.Username;
                var user = _userService.FindByUsername(username);
                if (user == null)
                {
                    throw new UserNotFoundException();
                }

                if (_accountDeleteConfigurationAccessor.Value.RespectEmailContactSetting && !user.EmailAllowed)
                {
                    throw new EmailContactNotAllowedException();
                }

                var recipientEmail = user.EmailAddress;
                var deleteSuccess = await _accountManager.DeleteAccount(user, source);
                _telemetryService.TrackDeleteResult(source, deleteSuccess);

                var baseEmailBuilder = _emailBuilderFactory.GetEmailBuilder(source, deleteSuccess);
                if (baseEmailBuilder != null)
                {
                    var toEmail = new List<MailAddress>();

                    var configuration = _accountDeleteConfigurationAccessor.Value;
                    var senderAddress = configuration.EmailConfiguration.GalleryOwner;
                    var ccEmail = new List<MailAddress>();
                    toEmail.Add(new MailAddress(recipientEmail));
                    ccEmail.Add(new MailAddress(senderAddress));

                    var recipients = new EmailRecipients(toEmail, ccEmail);
                    var emailBuilder = new DisposableEmailBuilder(baseEmailBuilder, recipients, username);
                    await _messenger.SendMessageAsync(emailBuilder);
                    _telemetryService.TrackEmailSent(source, user.EmailAllowed);
                    messageProcessed = true;
                }
            }
            catch (UnknownSourceException)
            {
                // Should definitely log if source isn't expected. Should we even send mail? or log and alert?
                // Log unknown source and fail.
                _logger.LogError("Unknown message source detected: {Source}.", command.Source);
                _telemetryService.TrackUnknownSource(source);
                messageProcessed = false;
            }
            catch (EmailContactNotAllowedException)
            {
                // Should we not send? or should we ignore the setting.
                _logger.LogWarning("User did not allow Email Contact.");
                _telemetryService.TrackEmailBlocked(source);
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("User was not found. They may have already been deleted.");
                _telemetryService.TrackUserNotFound(source);
                messageProcessed = true;
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "An unknown exception occured: {ExceptionMessage}");
                throw e;
            }

            return messageProcessed;
        }
    }
}
