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
            bool packageRegistrationAlreadyExists,
            HttpContextBase httpContext)
            : base(policies, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));

            PackageRegistrationAlreadyExists = packageRegistrationAlreadyExists;
        }

        public PackageSecurityPolicyEvaluationContext(
            IEntitiesContext entitiesContext,
            IPackageOwnershipManagementService packageOwnershipManagementService,
            IReservedNamespaceService reservedNamespaceService,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            bool packageRegistrationAlreadyExists,
            User sourceAccount,
            User targetAccount,
            HttpContextBase httpContext = null)
            : base(policies, sourceAccount, targetAccount, httpContext)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            EntitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            PackageOwnershipManagementService = packageOwnershipManagementService ?? throw new ArgumentNullException(nameof(packageOwnershipManagementService));
            ReservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));

            PackageRegistrationAlreadyExists = packageRegistrationAlreadyExists;
        }

        /// <summary>
        /// Package under evaluation.
        /// </summary>
        public Package Package { get; }

        /// <summary>
        /// <d>True</d> when the package registration already exists; otherwise <c>false</c>.
        /// </summary>
        public bool PackageRegistrationAlreadyExists { get; }

        public IEntitiesContext EntitiesContext { get; }

        public IPackageOwnershipManagementService PackageOwnershipManagementService { get; }

        public IReservedNamespaceService ReservedNamespaceService { get; }
    }
}