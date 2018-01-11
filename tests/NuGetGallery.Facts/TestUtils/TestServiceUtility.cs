using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery.TestUtils
{
    public static class PackageServiceUtility
    {
        private const string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

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
#pragma warning disable 0618
                Authors = new List<PackageAuthor>
                {
                    author
                },
#pragma warning restore 0618
                Dependencies = new List<PackageDependency>
                {
                    dependency
                },
                User = new User("test")
            };

            packageRegistration.Packages.Add(package);

            return package;
        }

        public static Mock<TestPackageReader> CreateNuGetPackage(
            string id = "theId",
            string version = "01.0.42.0",
            string title = "theTitle",
            string summary = "theSummary",
            string authors = "theFirstAuthor, theSecondAuthor",
            string owners = "Package owners",
            string description = "theDescription",
            string tags = "theTags",
            string language = null,
            string copyright = "theCopyright",
            string releaseNotes = "theReleaseNotes",
            string minClientVersion = null,
            Uri licenseUrl = null,
            Uri projectUrl = null,
            Uri iconUrl = null,
            bool requireLicenseAcceptance = true,
            IEnumerable<PackageDependencyGroup> packageDependencyGroups = null,
            IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes = null)
        {
            licenseUrl = licenseUrl ?? new Uri("http://thelicenseurl/");
            projectUrl = projectUrl ?? new Uri("http://theprojecturl/");
            iconUrl = iconUrl ?? new Uri("http://theiconurl/");

            if (packageDependencyGroups == null)
            {
                packageDependencyGroups = new[]
                {
                    new PackageDependencyGroup(
                        new NuGetFramework("net40"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "theFirstDependency",
                                VersionRange.Parse("[1.0.0, 2.0.0)")),

                            new NuGet.Packaging.Core.PackageDependency(
                                "theSecondDependency",
                                VersionRange.Parse("[1.0]")),

                            new NuGet.Packaging.Core.PackageDependency(
                                "theThirdDependency")
                        }),

                    new PackageDependencyGroup(
                        new NuGetFramework("net35"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "theFourthDependency",
                                VersionRange.Parse("[1.0]"))
                        })
                };
            }

            if (packageTypes == null)
            {
                packageTypes = new[]
                {
                    new NuGet.Packaging.Core.PackageType("dependency", new Version("1.0.0")),
                    new NuGet.Packaging.Core.PackageType("DotNetCliTool", new Version("2.1.1"))
                };
            }

            var testPackage = TestPackage.CreateTestPackageStream(
                id, version, title, summary, authors, owners,
                description, tags, language, copyright, releaseNotes,
                minClientVersion, licenseUrl, projectUrl, iconUrl,
                requireLicenseAcceptance, packageDependencyGroups, packageTypes);

            var mock = new Mock<TestPackageReader>(testPackage);
            mock.CallBase = true;
            return mock;
        }
    }

    public class TestableUserService : UserService
    {
        public Mock<IAppConfiguration> MockConfig { get; protected set; }
        public Mock<IEntityRepository<User>> MockUserRepository { get; protected set; }
        public Mock<IEntityRepository<Credential>> MockCredentialRepository { get; protected set; }
        public Mock<IEntitiesContext> MockEntitiesContext { get; protected set; }
        public Mock<IDatabase> MockDatabase { get; protected set; }

        public TestableUserService()
        {
            Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
            UserRepository = (MockUserRepository = new Mock<IEntityRepository<User>>()).Object;
            CredentialRepository = (MockCredentialRepository = new Mock<IEntityRepository<Credential>>()).Object;
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            Auditing = new TestAuditingService();

            // Set ConfirmEmailAddress to a default of true
            MockConfig.Setup(c => c.ConfirmEmailAddresses).Returns(true);

            MockDatabase = new Mock<IDatabase>();
            MockEntitiesContext.Setup(c => c.GetDatabase()).Returns(MockDatabase.Object);
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

        public override ReservedNamespace FindReservedNamespaceForPrefix(string prefix)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where request.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    select request).FirstOrDefault();
        }

        public override IReadOnlyCollection<ReservedNamespace> FindAllReservedNamespacesForPrefix(string prefix, bool getExactMatches)
        {
            Expression<Func<ReservedNamespace, bool>> prefixMatch;
            if (getExactMatches)
            {
                prefixMatch = dbPrefix => dbPrefix.Value.Equals(prefix, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                prefixMatch = dbPrefix => dbPrefix.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return ReservedNamespaceRepository.GetAll()
                .Where(prefixMatch)
                .ToList();
        }

        public override IReadOnlyCollection<ReservedNamespace> GetReservedNamespacesForId(string id)
        {
            return (from request in ReservedNamespaceRepository.GetAll()
                    where (request.IsPrefix && id.StartsWith(request.Value, StringComparison.OrdinalIgnoreCase))
                        || (!request.IsPrefix && id.Equals(request.Value, StringComparison.OrdinalIgnoreCase))
                    select request).ToList();
        }

        public override IReadOnlyCollection<ReservedNamespace> FindReservedNamespacesForPrefixList(IReadOnlyCollection<string> prefixList)
        {
            return (from dbPrefix in ReservedNamespaceRepository.GetAll()
                    where (prefixList.Any(p => p.Equals(dbPrefix.Value, StringComparison.OrdinalIgnoreCase)))
                    select dbPrefix).ToList();
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
            mockContext.Setup(m => m.GetDatabase())
                .Returns(new DatabaseWrapper(dbContext.Object.Database));

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
            var packageNamingConflictValidator = new PackageNamingConflictValidator(
                    packageRegistrationRepository.Object,
                    packageRepository.Object);
            var auditingService = new TestAuditingService();

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                packageNamingConflictValidator,
                auditingService);

            packageService.CallBase = true;

            return packageService;
        }
    }
}
