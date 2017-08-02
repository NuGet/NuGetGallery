using Moq;
using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Services
{
    public class ReservedNamespaceServiceFacts
    {
        private static IReservedNamespaceService CreateService(
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IEntityRepository<ReservedNamespace>> reservedNamespaceRepository = null,
            Mock<IUserService> userService = null,
            Mock<IPackageService> packageService = null,
            Mock<IAuditingService> auditingService = null,
            Action<Mock<ReservedNamespaceService>> setup = null)
        {
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            reservedNamespaceRepository = reservedNamespaceRepository ?? new Mock<IEntityRepository<ReservedNamespace>>();
            userService = userService ?? new Mock<IUserService>();
            packageService = packageService ?? new Mock<IPackageService>();
            auditingService = auditingService ?? new Mock<IAuditingService>();

            var reservedNamespaceService = new Mock<ReservedNamespaceService>(
                entitiesContext.Object,
                reservedNamespaceRepository.Object,
                userService.Object,
                packageService.Object,
                auditingService.Object);

            reservedNamespaceService.CallBase = true;
            if (setup != null)
            {
                setup(reservedNamespaceService);
            }

            return reservedNamespaceService.Object;
        }

        public class ReservedNamepsaceManagement
        {
            [Fact]
            public async Task NewPrefixIsAddedCorrectly()
            {
                var newNamespace = new ReservedNamespace("Microsoft.", isSharedNamespace: false, isExactMatch: false);
                await Task.Yield();
            }
        }
    }
}
