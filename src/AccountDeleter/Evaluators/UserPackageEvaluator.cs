// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluates a user's package ownership status
    /// </summary>
    public class UserPackageEvaluator : BaseUserEvaluator
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<UserPackageEvaluator> _logger;

        public UserPackageEvaluator(IPackageService packageService, ILogger<UserPackageEvaluator> logger)
            : base()
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanUserBeDeleted(User user)
        {
            var userPackages = _packageService.FindPackagesByOwner(user, includeUnlisted: false);

            if(userPackages.AnySafe())
            {
                _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because they owned listed packages.", nameof(UserPackageEvaluator), EvaluatorId);
                return false;
            }

            return true;
        }
    }
}
