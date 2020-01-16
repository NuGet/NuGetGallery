// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using NuGetGallery.Security;

namespace NuGetGallery.TestUtils
{
    public class TestableUserService : UserService
    {
        public Mock<IAppConfiguration> MockConfig { get; protected set; }
        public Mock<ISecurityPolicyService> MockSecurityPolicyService { get; protected set; }
        public Mock<IEntityRepository<User>> MockUserRepository { get; protected set; }
        public Mock<IEntityRepository<Role>> MockRoleRepository { get; protected set; }
        public Mock<IEntityRepository<Credential>> MockCredentialRepository { get; protected set; }
        public Mock<IEntityRepository<Organization>> MockOrganizationRepository { get; protected set; }
        public Mock<IEntitiesContext> MockEntitiesContext { get; protected set; }
        public Mock<IDatabase> MockDatabase { get; protected set; }
        public Mock<IContentObjectService> MockConfigObjectService { get; protected set; }
        public Mock<IDateTimeProvider> MockDateTimeProvider { get; protected set; }
        public Mock<ITelemetryService> MockTelemetryService { get; protected set; }
        public Mock<IDiagnosticsSource> MockDiagnosticsSource { get; protected set; }

        public TestableUserService()
        {
            Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
            SecurityPolicyService = (MockSecurityPolicyService = new Mock<ISecurityPolicyService>()).Object;
            UserRepository = (MockUserRepository = new Mock<IEntityRepository<User>>()).Object;
            RoleRepository = (MockRoleRepository = new Mock<IEntityRepository<Role>>()).Object;
            CredentialRepository = (MockCredentialRepository = new Mock<IEntityRepository<Credential>>()).Object;
            OrganizationRepository = (MockOrganizationRepository = new Mock<IEntityRepository<Organization>>()).Object;
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            ContentObjectService = (MockConfigObjectService = new Mock<IContentObjectService>()).Object;
            DateTimeProvider = (MockDateTimeProvider = new Mock<IDateTimeProvider>()).Object;
            Auditing = new TestAuditingService();
            TelemetryService = (MockTelemetryService = new Mock<ITelemetryService>()).Object;
            DiagnosticsSource = (MockDiagnosticsSource = new Mock<IDiagnosticsSource>()).Object;

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

        public IEnumerable<Role> Roles
        {
            set
            {
                foreach (Role r in value) FakeEntitiesContext.Set<Role>().Add(r);
            }
        }

        public TestableUserServiceWithDBFaking(FakeEntitiesContext context = null)
        {
            FakeEntitiesContext = context ?? new FakeEntitiesContext();
            Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
            UserRepository = new EntityRepository<User>(FakeEntitiesContext);
            RoleRepository = new EntityRepository<Role>(FakeEntitiesContext);
            Auditing = new TestAuditingService();
            TelemetryService = new TelemetryService(
                new Mock<IDiagnosticsSource>().Object,
                new Mock<ITelemetryClient>().Object);
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
            var database = new Mock<IDatabase>();
            database
                .Setup(x => x.BeginTransaction())
                .Returns(() => new Mock<IDbContextTransaction>().Object);
            mockContext
                .Setup(m => m.GetDatabase())
                .Returns(database.Object);

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
            var certificateRepository = new Mock<IEntityRepository<Certificate>>();
            var auditingService = new TestAuditingService();
            var telemetryService = new Mock<ITelemetryService>();
            var securityPolicyService = new Mock<ISecurityPolicyService>();

            var packageService = new Mock<PackageService>(
                 packageRegistrationRepository.Object,
                 packageRepository.Object,
                 certificateRepository.Object,
                 auditingService,
                 telemetryService.Object,
                 securityPolicyService.Object);

            packageService.CallBase = true;

            return packageService;
        }
    }
}
