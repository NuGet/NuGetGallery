using Moq;
using NuGetGallery.Auditing;
using System;
using System.Collections.Generic;
using System.Data.Entity;
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
                auditingService.Object)
            {
                CallBase = true
            };

            if (setup != null)
            {
                setup(reservedNamespaceService);
            }

            return reservedNamespaceService.Object;
        }

        public class ReservedNamespaceManagement
        {
            [Fact]
            public async Task NewNamespaceIsReservedCorrectly()
            {
                var newNamespace = new ReservedNamespace("NewNamespace.", isSharedNamespace: false, isPrefix: true);
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await service.AddReservedNamespaceAsync(newNamespace);

                regRepositiory.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                regRepositiory.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task ReservingNullNamespaceThrowsException()
            {
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task ReservingExistingNamespaceThrowsException()
            {
                var newNamespace = new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true);
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
                
            }

            [Fact]
            public async Task ReservingExistingNamespaceWithDifferentPrefixStateThrowsException()
            {
                var newNamespace = new ReservedNamespace("jQuery", isSharedNamespace: false, isPrefix: true);
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ReservingLiberalNamespaceThrowsException()
            {
                var newNamespace = new ReservedNamespace("Micro", isSharedNamespace: false, isPrefix: true);
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ReservingLiberalNamespaceForExactMatchIsAllowed()
            {
                var newNamespace = new ReservedNamespace("Microsoft", isSharedNamespace: false, isPrefix: false/*exact match*/);
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await service.AddReservedNamespaceAsync(newNamespace);

                regRepositiory.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                regRepositiory.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task NullNamespaceReservationThrowsException()
            {
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task VanillaReservedNamespaceIsDeletedCorrectly()
            {
                var prefixList = GetTestNamespaces();
                var existingNamespace = prefixList.SingleOrDefault(rn => rn.Value == "Microsoft.");

                var regRepositiory = SetupReservedNamespaceRepository();
                var entititesContext = SetupEntitiesContext();
                var service = CreateService(entitiesContext: entititesContext, reservedNamespaceRepository: regRepositiory);

                await service.DeleteReservedNamespaceAsync(existingNamespace);

                regRepositiory.Verify(
                    x => x.DeleteOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == existingNamespace.Value
                                && rn.IsPrefix == existingNamespace.IsPrefix
                                && rn.IsSharedNamespace == existingNamespace.IsSharedNamespace)));

                regRepositiory.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task NullNamespaceDeletionThrowsException()
            {
                var regRepositiory = SetupReservedNamespaceRepository();
                var service = CreateService(reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeleteReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task NonExistingNamespaceDeletionThrowsException()
            {
                var nonExistentNamespace = new ReservedNamespace("NewNamespace.", isSharedNamespace: false, isPrefix: true);
                var regRepositiory = SetupReservedNamespaceRepository();
                var entititesContext = SetupEntitiesContext();
                var service = CreateService(entitiesContext: entititesContext, reservedNamespaceRepository: regRepositiory);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteReservedNamespaceAsync(nonExistentNamespace));
            }

            private static IList<ReservedNamespace> GetTestNamespaces()
            {
                var result = new List<ReservedNamespace>();
                result.Add(new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true));
                result.Add(new ReservedNamespace("Microsoft.AspNet.", isSharedNamespace: false, isPrefix: true));
                result.Add(new ReservedNamespace("BaseTest.", isSharedNamespace: false, isPrefix: true));
                result.Add(new ReservedNamespace("jQuery", isSharedNamespace: false, isPrefix: false));
                result.Add(new ReservedNamespace("jQuery.Extentions.", isSharedNamespace: true, isPrefix: true));
                result.Add(new ReservedNamespace("Random.", isSharedNamespace: false, isPrefix: true));

                return result;
            }

            private static Mock<IEntityRepository<ReservedNamespace>> SetupReservedNamespaceRepository()
            {
                var obj = new Mock<IEntityRepository<ReservedNamespace>>();
                var prefixList = GetTestNamespaces();

                obj.Setup(x => x.GetAll())
                    .Returns(prefixList.AsQueryable());

                return obj;
            }
            private static Mock<UserService> SetupUserService()
            {
                return null;
            }
            private static Mock<PackageService> SetupPackageService()
            {
                return null;
            }
            private static Mock<AuditingService> SetupAuditingService()
            {
                return null;
            }

            private static Mock<IEntitiesContext> SetupEntitiesContext()
            {
                var obj = new Mock<IEntitiesContext>();
                var dbContext = new Mock<DbContext>();

                obj
                    .Setup(m => m.GetDatabase())
                    .Returns(dbContext.Object.Database);

                return obj;
            }
        }
    }
}
