// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Packaging;
using NuGetGallery.Packaging;
using System.Threading.Tasks;
using System.Collections.Generic;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using System.Data.Entity;

namespace NuGetGallery
{
    public class PackageOwnershipManagementServiceFacts
    {
        private static PackageOwnershipManagementService CreateService(
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IPackageService> packageService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IPackageOwnerRequestService> validationService = null,
            IAuditingService auditingService = null)
        {
            var dbContext = new Mock<DbContext>();
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            entitiesContext.Setup(m => m.GetDatabase()).Returns(dbContext.Object.Database);

            packageService = new Mock<IPackageService>();

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();

                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService
                    .Setup(s => s.IsPushAllowed(It.IsAny<string>(), It.IsAny<User>(), out userOwnedMatchingNamespaces))
                    .Returns(true);
            }

            auditingService = auditingService ?? new TestAuditingService();

            var packageOwnershipManagementService = new Mock<PackageOwnershipManagementService>(
                entitiesContext.Object,
                packageService.Object,
                reservedNamespaceService.Object,
                auditingService);

            return packageOwnershipManagementService.Object;
        }

        public class TheAddPackageOwnerAsyncMethod
        {
        }

        public class TheAddPendingOwnershipRequestAsyncMethod
        {
        }

        public class TheRemovePackageOwnerAsyncMethod
        {
        }

        public class TheRemovePendingOwnershipRequestAsync
        {
        }
    }
}