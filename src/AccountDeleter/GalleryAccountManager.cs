﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using System;
using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public class GalleryAccountManager : IAccountManager
    {
        private readonly IOptionsSnapshot<AccountDeleteConfiguration> _accountDeleteConfigurationAccessor;
        private readonly IDeleteAccountService _deleteAccountService;
        private readonly IUserEvaluatorFactory _userEvaluatorFactory;
        private readonly IAccountDeleteTelemetryService _telemetryService;
        private readonly ILogger<GalleryAccountManager> _logger;

        public GalleryAccountManager(
            IOptionsSnapshot<AccountDeleteConfiguration> accountDeleteConfigurationAccessor,
            IDeleteAccountService deleteAccountService,
            IUserEvaluatorFactory userEvaluatorFactory,
            IAccountDeleteTelemetryService telemetryService,
            ILogger<GalleryAccountManager> logger)
        {
            _accountDeleteConfigurationAccessor = accountDeleteConfigurationAccessor ?? throw new ArgumentNullException(nameof(accountDeleteConfigurationAccessor));
            _deleteAccountService = deleteAccountService ?? throw new ArgumentNullException(nameof(deleteAccountService));
            _userEvaluatorFactory = userEvaluatorFactory ?? throw new ArgumentNullException(nameof(userEvaluatorFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> DeleteAccount(User user, string source)
        {
            _logger.LogInformation("Attempting delete...");
            if (user == null)
            {
                _logger.LogWarning("Requested user was not found in DB. Delete was probably already done.");
                throw new UserNotFoundException();
            }

            var evaluator = _userEvaluatorFactory.GetEvaluatorForSource(source);

            if (!evaluator.CanUserBeDeleted(user))
            {
                _logger.LogInformation("User was not able to be automatically deleted. Criteria check failed.");
                return false;
            }

            _logger.LogInformation("All criteria passed.");
            var result = await _deleteAccountService.DeleteAccountAsync(userToBeDeleted: user, userToExecuteTheDelete: user, orphanPackagePolicy: AccountDeletionOrphanPackagePolicy.UnlistOrphans);
            if (result.Success)
            {
                _logger.LogInformation("Deleted user successfully.");
                return true;
            }
            else
            {
                _logger.LogError("Criteria passed but delete failed.");
                _logger.LogError(result.Description);
                return false;
            }
        }
    }
}
