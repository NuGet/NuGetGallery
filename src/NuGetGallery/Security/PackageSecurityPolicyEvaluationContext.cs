// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using NuGet.Services.Entities;
using NuGetGallery.Services.PackageManagement;
using NuGetGallery.Services.Security;
using NuGetGallery.Services.Telemetry;
using NuGetGallery.Services.UserManagement;

namespace NuGetGallery.Security
{
    public class PackageSecurityPolicyEvaluationContext : UserSecurityPolicyEvaluationContext
    {
        public PackageSecurityPolicyEvaluationContext(
            IUserService userService,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            ITelemetryService telemetryService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            HttpContextBase httpContext)
            : base(policies, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            UserService = userService ?? throw new ArgumentNullException(nameof(userService));
            TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
        }

        public PackageSecurityPolicyEvaluationContext(
            IUserService userService,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            ITelemetryService telemetryService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null)
            : base(policies, sourceAccount, targetAccount, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            UserService = userService ?? throw new ArgumentNullException(nameof(userService));
            TelemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
        }

        /// <summary>
        /// Package under evaluation.
        /// </summary>
        public Package Package { get; }

        public IUserService UserService { get; }

        public ITelemetryService TelemetryService { get; }

        public IPackageOwnershipManagementService PackageOwnershipManagementService { get; }
    }
}