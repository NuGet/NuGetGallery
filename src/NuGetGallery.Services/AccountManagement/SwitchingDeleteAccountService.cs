// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery
{
    /// <summary>
    /// Allows on the fly swapping between synchronous and asynchronous account delete flow. Switched on feature flags.
    /// </summary>
    public class SwitchingDeleteAccountService : IDeleteAccountService
    {

        private IDeleteAccountService _syncDeleteService;
        private IDeleteAccountService _asyncDeleteService;
        private IFeatureFlagService _featureFlagService;
        private ILogger<SwitchingDeleteAccountService> _logger;

        public SwitchingDeleteAccountService(
            IDeleteAccountService syncDeleteService, 
            IDeleteAccountService asyncDeleteService, 
            IFeatureFlagService featureFlagService, 
            ILogger<SwitchingDeleteAccountService> logger)
        {
            _syncDeleteService = syncDeleteService ?? throw new ArgumentException(nameof(syncDeleteService));
            _asyncDeleteService = asyncDeleteService ?? throw new ArgumentException(nameof(asyncDeleteService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _featureFlagService = featureFlagService;
        }

        public async Task<DeleteAccountStatus> DeleteAccountAsync(User userToBeDeleted, User userToExecuteTheDelete, AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans)
        {
            if (_featureFlagService != null && _featureFlagService.IsAsyncAccountDeleteEnabled())
            {
                return await _asyncDeleteService.DeleteAccountAsync(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
            }

            return await _syncDeleteService.DeleteAccountAsync(userToBeDeleted, userToExecuteTheDelete, orphanPackagePolicy);
        }
    }
}
