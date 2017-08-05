using Moq;
using NuGet.Packaging;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
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
            Mock<PackageService> packageService = null,
            Mock<IAuditingService> auditingService = null,
            Action<Mock<ReservedNamespaceService>> setup = null)
        {
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            reservedNamespaceRepository = reservedNamespaceRepository ?? new Mock<IEntityRepository<ReservedNamespace>>();
            userService = userService ?? new Mock<IUserService>();
            packageService = packageService ?? new Mock<PackageService>();
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
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await service.AddReservedNamespaceAsync(newNamespace);

                rnRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                rnRepository.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task ReservingNullNamespaceThrowsException()
            {
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task ReservingExistingNamespaceThrowsException()
            {
                var newNamespace = new ReservedNamespace("Microsoft.", isSharedNamespace: false, isPrefix: true);
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));

            }

            [Fact]
            public async Task ReservingExistingNamespaceWithDifferentPrefixStateThrowsException()
            {
                var newNamespace = new ReservedNamespace("jQuery", isSharedNamespace: false, isPrefix: true);
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ReservingLiberalNamespaceThrowsException()
            {
                var newNamespace = new ReservedNamespace("Micro", isSharedNamespace: false, isPrefix: true);
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.AddReservedNamespaceAsync(newNamespace));
            }

            [Fact]
            public async Task ReservingLiberalNamespaceForExactMatchIsAllowed()
            {
                var newNamespace = new ReservedNamespace("Microsoft", isSharedNamespace: false, isPrefix: false/*exact match*/);
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await service.AddReservedNamespaceAsync(newNamespace);

                rnRepository.Verify(
                    x => x.InsertOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == newNamespace.Value
                                && rn.IsPrefix == newNamespace.IsPrefix
                                && rn.IsSharedNamespace == newNamespace.IsSharedNamespace)));

                rnRepository.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task NullNamespaceReservationThrowsException()
            {
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task VanillaReservedNamespaceIsDeletedCorrectly()
            {
                var prefixList = GetTestNamespaces();
                var existingNamespace = prefixList.SingleOrDefault(rn => rn.Value == "Microsoft.");

                var rnRepository = SetupReservedNamespaceRepository();
                var entititesContext = SetupEntitiesContext();
                var packageService = SetupPackageService();

                var service = CreateService(entitiesContext: entititesContext, reservedNamespaceRepository: rnRepository, packageService: packageService);

                await service.DeleteReservedNamespaceAsync(existingNamespace);

                rnRepository.Verify(
                    x => x.DeleteOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == existingNamespace.Value
                                && rn.IsPrefix == existingNamespace.IsPrefix
                                && rn.IsSharedNamespace == existingNamespace.IsSharedNamespace)));

                rnRepository.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task NullNamespaceDeletionThrowsException()
            {
                var rnRepository = SetupReservedNamespaceRepository();
                var packageService = SetupPackageService();

                var service = CreateService(reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeleteReservedNamespaceAsync(null));
            }

            [Fact]
            public async Task NonExistingNamespaceDeletionThrowsException()
            {
                var nonExistentNamespace = new ReservedNamespace("NewNamespace.", isSharedNamespace: false, isPrefix: true);
                var rnRepository = SetupReservedNamespaceRepository();
                var entititesContext = SetupEntitiesContext();
                var packageService = SetupPackageService();

                var service = CreateService(entitiesContext: entititesContext, reservedNamespaceRepository: rnRepository, packageService: packageService);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteReservedNamespaceAsync(nonExistentNamespace));
            }

            [Fact]
            public async Task DeletingNamespaceClearsVerifiedFlagOnPackage()
            {
                var namespaces = GetTestNamespaces();
                var registrations = GetRegistrations();
                var msPrefix = namespaces.First(x => x.Value == "Microsoft.");
                msPrefix.PackageRegistrations = registrations.Where(x => x.Id.StartsWith(msPrefix.Value)).ToList();
                msPrefix.PackageRegistrations.ToList().ForEach(x => x.ReservedNamespaces.Add(msPrefix));

                var entititesContext = SetupEntitiesContext();
                var rnRepository = SetupReservedNamespaceRepository(namespaces);
                var packageService = SetupPackageService(registrations);

                var service = CreateService(entitiesContext: entititesContext, reservedNamespaceRepository: rnRepository, packageService: packageService);

                await service.DeleteReservedNamespaceAsync(msPrefix);

                var registrationsShouldUpdate = msPrefix.PackageRegistrations;
                Assert.True(registrationsShouldUpdate.Count() > 0);
                rnRepository.Verify(
                    x => x.DeleteOnCommit(
                        It.Is<ReservedNamespace>(
                            rn => rn.Value == msPrefix.Value
                                && rn.IsPrefix == msPrefix.IsPrefix
                                && rn.IsSharedNamespace == msPrefix.IsSharedNamespace)));

                rnRepository.Verify(x => x.CommitChangesAsync());
                packageService.Verify(
                    p => p.UpdatePackageVerifiedStatusAsync(
                        It.Is<IList<PackageRegistration>>(
                            list => list.Count() == registrationsShouldUpdate.Count()
                                && list.Any(pr => registrationsShouldUpdate.Any(ru => ru.Id == pr.Id))),
                        false),
                    Times.Once);

                msPrefix.PackageRegistrations.ToList().ForEach(rn => Assert.False(rn.IsVerified));
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

            private static IList<PackageRegistration> GetRegistrations()
            {
                var result = new List<PackageRegistration>();
                result.Add(new PackageRegistration { Id = "Microsoft.Package1", IsVerified = true });
                result.Add(new PackageRegistration { Id = "Microsoft.AspNet.Package2", IsVerified = true });
                result.Add(new PackageRegistration { Id = "Random.Package1", IsVerified = true });
                result.Add(new PackageRegistration { Id = "jQuery", IsVerified = true });
                result.Add(new PackageRegistration { Id = "jQuery.Extentions.OwnerView", IsVerified = true });
                result.Add(new PackageRegistration { Id = "jQuery.Extentions.ThirdPartyView", IsVerified = false });
                result.Add(new PackageRegistration { Id = "DeltaX.Test1", IsVerified = false });

                return result;
            }

            private static Mock<IEntityRepository<ReservedNamespace>> SetupReservedNamespaceRepository(IList<ReservedNamespace> prefixList = null)
            {
                var obj = new Mock<IEntityRepository<ReservedNamespace>>();
                prefixList = prefixList ?? GetTestNamespaces();

                obj.Setup(x => x.GetAll())
                    .Returns(prefixList.AsQueryable());

                return obj;
            }

            private static Mock<UserService> SetupUserService()
            {
                return null;
            }

            private static Mock<PackageService> SetupPackageService(IList<PackageRegistration> registrations = null)
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var registrationList = registrations != null ? registrations.AsQueryable() : null;
                packageRegistrationRepository
                    .Setup(x => x.GetAll())
                    .Returns(registrationList)
                    .Verifiable();

                var packageRepository = new Mock<IEntityRepository<Package>>();
                var packageOwnerRequestRepo = new Mock<IEntityRepository<PackageOwnerRequest>>();
                var indexingService = new Mock<IIndexingService>();
                var packageNamingConflictValidator = new PackageNamingConflictValidator(
                        packageRegistrationRepository.Object,
                        packageRepository.Object);
                var auditingService = new TestAuditingService();

                var packageService = new Mock<PackageService>(
                    packageRegistrationRepository.Object,
                    packageRepository.Object,
                    packageOwnerRequestRepo.Object,
                    indexingService.Object,
                    packageNamingConflictValidator,
                    auditingService);

                packageService.CallBase = true;

                //packageService
                //    .Setup(s => s.UpdatePackageVerifiedStatusAsync(
                //        It.IsAny<IList<PackageRegistration>>(),
                //        It.IsAny<bool>()))
                //    .Returns(Task.CompletedTask)
                //    .Verifiable();

                return packageService;
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
