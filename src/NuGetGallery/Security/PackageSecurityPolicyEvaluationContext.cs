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
            IReservedNamespaceService reservedNamespaceService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            PackageRegistration existingPackageRegistration,
            HttpContextBase httpContext)
            : base(policies, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));

            ExistingPackageRegistration = existingPackageRegistration;
        }

        public PackageSecurityPolicyEvaluationContext(
            IEntitiesContext entitiesContext,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IReservedNamespaceService reservedNamespaceService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            PackageRegistration existingPackageRegistration,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null)
            : base(policies, sourceAccount, targetAccount, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));

            ExistingPackageRegistration = existingPackageRegistration;
        }

        /// <summary>
        /// Package under evaluation.
        /// </summary>
        public Package Package { get; }

        /// <summary>
        /// The existing package registration for the package under evaluation,
        /// or <code>null</code> if the <see cref="Package"/> has not been registered yet.
        /// </summary>
        public PackageRegistration ExistingPackageRegistration { get; }

        public IEntitiesContext EntitiesContext { get; }

        public IPackageOwnershipManagementService PackageOwnershipManagementService { get; }

        public IReservedNamespaceService ReservedNamespaceService { get; }
    }
}