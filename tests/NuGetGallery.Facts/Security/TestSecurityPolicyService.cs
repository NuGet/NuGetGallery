// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Security
{
    public class TestSecurityPolicyService : SecurityPolicyService
    {
        public Mock<IAuditingService> MockAuditingService { get; }

        public Mock<IEntitiesContext> MockEntitiesContext { get; }

        public IAppConfiguration AppConfiguration { get; }

        public Mock<DbSet<UserSecurityPolicy>> MockUserSecurityPolicies { get; }

        public TestUserSecurityPolicyData Mocks { get; }

        public override IEnumerable<IUserSecurityPolicySubscription> Subscriptions { get; }

        protected override IEnumerable<UserSecurityPolicyHandler> UserHandlers { get; }

        public TestSecurityPolicyService(
            TestUserSecurityPolicyData mocks = null,
            IEnumerable<UserSecurityPolicyHandler> userHandlers = null,
            IEnumerable<IUserSecurityPolicySubscription> userSubscriptions = null,
            IEnumerable<IUserSecurityPolicySubscription> organizationSubscriptions = null,
            Mock<IEntitiesContext> mockEntities = null,
            Mock<IAuditingService> mockAuditing = null,
            IAppConfiguration configuration = null)
            : this(mockEntities, mockAuditing, configuration)
        {
            Mocks = mocks ?? new TestUserSecurityPolicyData();

            UserHandlers = userHandlers ?? Mocks.Handlers.Select(m => m.Object);
            Subscriptions = userSubscriptions ?? new[] { Mocks.UserPoliciesSubscription.Object };
            DefaultSubscription = Mocks.DefaultSubscription.Object;
        }

        protected TestSecurityPolicyService(
            Mock<IEntitiesContext> mockEntities,
            Mock<IAuditingService> mockAuditing,
            IAppConfiguration configuration)
        {
            MockUserSecurityPolicies = new Mock<DbSet<UserSecurityPolicy>>();
            MockUserSecurityPolicies.Setup(p => p.Remove(It.IsAny<UserSecurityPolicy>())).Verifiable();

            MockEntitiesContext = mockEntities ?? new Mock<IEntitiesContext>();
            if (mockEntities == null)
            {
                MockEntitiesContext.Setup(c => c.SaveChangesAsync()).Returns(Task.FromResult(2)).Verifiable();
                MockEntitiesContext.Setup(c => c.UserSecurityPolicies).Returns(MockUserSecurityPolicies.Object);
            }
            EntitiesContext = MockEntitiesContext.Object;

            MockAuditingService = mockAuditing ?? new Mock<IAuditingService>();
            if (mockAuditing == null)
            {
                MockAuditingService.Setup(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>()))
                    .Returns(Task.CompletedTask).Verifiable();
            }
            Auditing = MockAuditingService.Object;

            Configuration = configuration ?? new AppConfiguration();

            var telemetryClient = new Mock<ITelemetryClient>().Object;

            Diagnostics = new TraceDiagnosticsSource(
                nameof(TestSecurityPolicyService),
                telemetryClient);
        }
    }
}