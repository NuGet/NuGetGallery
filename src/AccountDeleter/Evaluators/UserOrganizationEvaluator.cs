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
        private readonly IPackageService _packageService;
        private readonly ILogger<UserPackageEvaluator> _logger;

        public UserOrganizationEvaluator(IPackageService packageService, ILogger<UserPackageEvaluator> logger)
            : base()
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanUserBeDeleted(User user)
        {
            throw new NotImplementedException();
        }
    }
}