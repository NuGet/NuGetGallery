// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Simple single (non aggregate) evaluator for easy use
    /// Criteria is user must own zero (0) packages, and be admin on zero (0) organizations. OR User is unconfirmed
    /// </summary>
    public class NuGetDeleteEvaluator : BaseUserEvaluator
    {
        private IPackageService _packageService;
        private ILogger<NuGetDeleteEvaluator> _logger;

        public NuGetDeleteEvaluator(IPackageService packageService, ILogger<NuGetDeleteEvaluator> logger)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(logger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanUserBeDeleted(User user)
        {
            var deleteUser = true;

            if (!user.Confirmed)
            {
                return deleteUser;
            }

            var userPackages = _packageService.FindPackagesByOwner(user, includeUnlisted: true);

            if (userPackages.AnySafe())
            {
                _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because they owned packages.", nameof(NuGetDeleteEvaluator), EvaluatorId);
                deleteUser = false;
            }

            var userOrgMemberships = user.Organizations;
            var userIsAdminOnOrgs = userOrgMemberships.AnySafe(m => m.IsAdmin);

            if (userIsAdminOnOrgs)
            {
                _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because they are administrator on an organization.", nameof(UserOrganizationEvaluator), EvaluatorId);
                deleteUser = false;
            }

            return deleteUser;
        }
    }
}
