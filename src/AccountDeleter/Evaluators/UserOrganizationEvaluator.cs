// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluates a user's organization memebership (WIP)
    /// </summary>
    public class UserOrganizationEvaluator : BaseUserEvaluator
    {
        private readonly ILogger<UserOrganizationEvaluator> _logger;

        public UserOrganizationEvaluator(ILogger<UserOrganizationEvaluator> logger)
            : base()
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanUserBeDeleted(User user)
        {
            var userOrgMemberships = user.Organizations;

            var userIsAdminOnOrgs = userOrgMemberships.AnySafe(m => m.IsAdmin);

            if (userIsAdminOnOrgs)
            {
                _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because they are administrator on an organization.", nameof(UserOrganizationEvaluator), EvaluatorId);
                return false;
            }

            return true;

        }
    }
}