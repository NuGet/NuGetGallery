// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery.Security
{
    public class PackageSecurityPolicyEvaluationContext : UserSecurityPolicyEvaluationContext
    {
        public PackageSecurityPolicyEvaluationContext(
            IEntitiesContext entitiesContext,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            HttpContextBase httpContext)
            : base(policies, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
        }

        public PackageSecurityPolicyEvaluationContext(
            IEntitiesContext entitiesContext,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null)
            : base(policies, sourceAccount, targetAccount, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
        }

        /// <summary>
        /// Package under evaluation.
        /// </summary>
        public Package Package { get; }

        public IEntitiesContext EntitiesContext { get; }

        public IPackageOwnershipManagementService PackageOwnershipManagementService { get; }
    }
}