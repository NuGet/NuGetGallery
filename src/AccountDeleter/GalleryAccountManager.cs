// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;
using System.Linq;

namespace NuGetGallery.AccountDeleter
{
    public class GalleryAccountManager : IAccountManager
    {
        private readonly IDeleteAccountService _deleteAccountService;
        private readonly IUserService _userService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<GalleryAccountManager> _logger;

        public GalleryAccountManager(
            IDeleteAccountService deleteAccountService,
            IUserService userService,
            IUserEvaluator userEvaluator,
            ITelemetryService telemetryService,
            ILogger<GalleryAccountManager> logger)
        {
            _deleteAccountService = deleteAccountService ?? throw new ArgumentNullException(nameof(deleteAccountService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool DeleteAccount(string username)
        {
            var user = _userService.FindByUsername(username);
            if (ShouldDeleteAccount(user))
            {
                _deleteAccountService.DeleteAccountAsync(user, user, AccountDeletionOrphanPackagePolicy.UnlistOrphans);
                _telemetryService.TrackAccountDelete();
                return true;
            }

            return false;
        }

        private bool ShouldDeleteAccount(User user)
        {
            return false;
        }
    }
}
