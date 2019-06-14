// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    public class UserPackageEvaluator : IUserEvaluator
    {
        private readonly Guid _id;
        private readonly IPackageService _packageService;
        private readonly IAccountDeleteTelemetryService _telemtryService;
        private readonly ILogger<UserPackageEvaluator> _logger;

        public UserPackageEvaluator(IPackageService packageService, IAccountDeleteTelemetryService telemetryService, ILogger<UserPackageEvaluator> logger)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _telemtryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _id = Guid.NewGuid();
        }

        public string EvaluatorId {
            get
            {
                return _id.ToString();
            }
        }

        public bool CanUserBeDeleted(User user)
        {
            // Current implementation is not allowed if user owns any listed packages
            return !_packageService.FindPackagesByOwner(user, includeUnlisted: false).AnySafe();
        }
    }
}
