using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery.TestUtils
{
    public static class PackageServiceUtility
    {
        private static readonly string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

        public static Package CreateTestPackage(string id = null)
        {
            var packageRegistration = new PackageRegistration();
            packageRegistration.Id = string.IsNullOrEmpty(id) ? "test" : id;

            var framework = new PackageFramework();
            var author = new PackageAuthor { Name = "maarten" };
            var dependency = new PackageDependency { Id = "other", VersionSpec = "1.0.0" };

            var package = new Package
            {
                Key = 123,
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Hash = _packageHashForTests,
                SupportedFrameworks = new List<PackageFramework>
                {
                    framework
                },
                FlattenedAuthors = "maarten",
                Authors = new List<PackageAuthor>
                {
                    author
                },
                Dependencies = new List<PackageDependency>
                {
                    dependency
                },
                User = new User("test")
            };

            packageRegistration.Packages.Add(package);

            return package;
        }
    }

    public class TestableUserService : UserService
    {
        public Mock<IAppConfiguration> MockConfig { get; protected set; }
        public Mock<IEntityRepository<User>> MockUserRepository { get; protected set; }
        public Mock<IEntityRepository<Credential>> MockCredentialRepository { get; protected set; }

        public TestableUserService()
        {
            Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
            UserRepository = (MockUserRepository = new Mock<IEntityRepository<User>>()).Object;
            CredentialRepository = (MockCredentialRepository = new Mock<IEntityRepository<Credential>>()).Object;
            Auditing = new TestAuditingService();

            // Set ConfirmEmailAddress to a default of true
            MockConfig.Setup(c => c.ConfirmEmailAddresses).Returns(true);
        }
    }

    public class TestableUserServiceWithDBFaking : UserService
    {
        public Mock<IAppConfiguration> MockConfig { get; protected set; }

        public FakeEntitiesContext FakeEntitiesContext { get; set; }

        public IEnumerable<User> Users
        {
            set
            {
                foreach (User u in value) FakeEntitiesContext.Set<User>().Add(u);
            }
        }

        public TestableUserServiceWithDBFaking(FakeEntitiesContext context = null)
        {
            FakeEntitiesContext = context ?? new FakeEntitiesContext();
            Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
            UserRepository = new EntityRepository<User>(FakeEntitiesContext);
            Auditing = new TestAuditingService();
        }
    }

    public class TestableReservedNamespaceService : ReservedNamespaceService
    {
        public Mock<PackageService> MockPackageService;
        public Mock<IEntityRepository<ReservedNamespace>> MockReservedNamespaceRepository;

        public IEnumerable<ReservedNamespace> ReservedNamespaces;
        public IEnumerable<PackageRegistration> PackageRegistrations;
        public IEnumerable<User> Users;

        public TestableReservedNamespaceService(
            IList<ReservedNamespace> reservedNamespaces = null,
            IList<PackageRegistration> packageRegistrations = null,
            IList<User> users = null)
        {
            ReservedNamespaces = reservedNamespaces ?? new List<ReservedNamespace>();
            PackageRegistrations = packageRegistrations ?? new List<PackageRegistration>();
            Users = users ?? new List<User>();

            EntitiesContext = SetupEntitiesContext().Object;

            MockReservedNamespaceRepository = SetupReservedNamespaceRepository();
            ReservedNamespaceRepository = MockReservedNamespaceRepository.Object;

            MockPackageService = SetupPackageService();
            PackageService = MockPackageService.Object;

            UserService = new TestableUserServiceWithDBFaking();
            ((TestableUserServiceWithDBFaking)UserService).Users = Users;

            AuditingService = new TestAuditingService();
        }
        
        private Mock<IEntityRepository<ReservedNamespace>> SetupReservedNamespaceRepository()
        {
            var obj = new Mock<IEntityRepository<ReservedNamespace>>();

            obj.Setup(x => x.GetAll())
                .Returns(ReservedNamespaces.AsQueryable());

            return obj;
        }

        private Mock<IEntitiesContext> SetupEntitiesContext()
        {
            var mockContext = new Mock<IEntitiesContext>();
            var dbContext = new Mock<DbContext>();
            mockContext.Setup(m => m.GetDatabase()).Returns(dbContext.Object.Database);

            return mockContext;
        }

        private Mock<PackageService> SetupPackageService()
        {
            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(PackageRegistrations.AsQueryable())
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

            return packageService;
        }
    }
}
