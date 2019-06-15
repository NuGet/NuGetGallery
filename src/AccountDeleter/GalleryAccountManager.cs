// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public class GalleryAccountManager : IAccountManager
    {
        private readonly IOptionsSnapshot<AccountDeleteConfiguration> _options;
        private readonly IDeleteAccountService _deleteAccountService;
        private readonly IUserService _userService;
        private readonly IUserEvaluator _userEvaluator;
        private readonly IAccountDeleteTelemetryService _telemetryService;
        private readonly ILogger<GalleryAccountManager> _logger;

        public GalleryAccountManager(
            IOptionsSnapshot<AccountDeleteConfiguration> options,
            IDeleteAccountService deleteAccountService,
            IUserService userService,
            IUserEvaluator userEvaluator,
            IAccountDeleteTelemetryService telemetryService,
            ILogger<GalleryAccountManager> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _deleteAccountService = deleteAccountService ?? throw new ArgumentNullException(nameof(deleteAccountService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _userEvaluator = userEvaluator ?? throw new ArgumentNullException(nameof(userEvaluator));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> DeleteAccount(string username)
        {
            _logger.LogInformation("Attempting delete...");
            var user = _userService.FindByUsername(username);
            if (user == null)
            {
                _logger.LogWarning("Requested user was not found in DB. Assuming delete was already done.");
                return true;
            }

            if (_userEvaluator.CanUserBeDeleted(user))
            {
                _logger.LogInformation("All criteria passed.");
                var result = await _deleteAccountService.DeleteAccountAsync(user, user, AccountDeletionOrphanPackagePolicy.UnlistOrphans);
                if (result.Success)
                {
                    _logger.LogInformation("Deleted user successfully.");
                    _telemetryService.TrackAccountDelete();
                    return true;
                }
                else
                {
                    _logger.LogError("Criteria passed but delete failed.");
                    _telemetryService.TrackAccountDelete();
                    return false;
                }
            }

            _logger.LogInformation("User was not able to be automatically deleted. Criteria check failed.");
            return false;
        }

        public Task<string> GetEmailAddresForUser(string username)
        {
            var user = _userService.FindByUsername(username);
            // We may want to ignore this setting, but respect contact for now
            if(!user.EmailAllowed)
            {
                _logger.LogWarning("User did not allow contact by email.");

                if (_options.Value.RespectEmailContactSetting)
                {
                    throw new EmailContactNotAllowedException();
                }
                else
                {
                    _logger.LogWarning("Ignoring EmailAllowed due to configuration.");
                }
            }

            return Task.FromResult(user.EmailAddress ?? user.UnconfirmedEmailAddress);
        }
    }
}
