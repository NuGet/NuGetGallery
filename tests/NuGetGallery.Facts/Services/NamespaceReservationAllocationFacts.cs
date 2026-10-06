// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Services
{
    public class NamespaceReservationAllocationFacts
    {
        [Fact]
        public async Task SavesPrivatePairTrackedOwnersAndOnlyMatchingOwnedPackagesTogetherThenAudits()
        {
            var fixture = new Fixture();
            var exact = fixture.AddPackage(10, "Contoso.Tools", 1);
            var child = fixture.AddPackage(11, "Contoso.Tools.Child", 2);
            var shared = fixture.AddPackage(12, "Contoso.Tools.Shared", 1, 2);
            fixture.Registrations.Add(shared); // Duplicate result and co-owned package.
            fixture.AddPackage(13, "Contoso.ToolsExtra", 1);
            fixture.AddPackage(14, "Contoso.Tools-Other", 2);
            fixture.AddPackage(15, "Contoso", 1);
            fixture.AddPackage(16, "Contoso.Tools.NotOwned", 3);
            fixture.AddPackage(17, "Contoso.Toolset.Child", 1);
            fixture.BeforeSave = () =>
            {
                Assert.Equal(2, fixture.Staged.Count);
                Assert.All(fixture.Staged, allocation =>
                {
                    Assert.False(allocation.IsSharedNamespace);
                    Assert.Equal(new[] { 1, 2 }, allocation.Owners.Select(owner => owner.Key).OrderBy(key => key));
                    Assert.All(allocation.Owners, owner => Assert.Same(fixture.TrackedUsers.Single(user => user.Key == owner.Key), owner));
                    Assert.DoesNotContain(allocation.Owners, owner => fixture.FreshUsers.Any(user => ReferenceEquals(user, owner)));
                });
                Assert.All(new[] { exact, child, shared }, package => Assert.True(package.IsVerified));
                Assert.All(fixture.Registrations.Where(package => package.Key >= 13), package => Assert.False(package.IsVerified));
                Assert.Empty(fixture.Durable);
                Assert.Empty(fixture.Audits);
            };

            await fixture.AllocateAsync(new[] { 1, 2, 1, 2 });

            fixture.AssertSuccess();
            Assert.Collection(fixture.Durable,
                allocation =>
                {
                    Assert.Equal("Contoso.Tools", allocation.Value);
                    Assert.False(allocation.IsPrefix);
                    Assert.Equal(new[] { 10 }, allocation.PackageKeys);
                },
                allocation =>
                {
                    Assert.Equal("Contoso.Tools.", allocation.Value);
                    Assert.True(allocation.IsPrefix);
                    Assert.Equal(new[] { 11, 12 }, allocation.PackageKeys);
                });
            Assert.Equal(new[] { 10, 11, 12 }, fixture.DurableVerification.Where(pair => pair.Value).Select(pair => pair.Key).OrderBy(key => key));
            fixture.Packages.Verify(service => service.UpdatePackageVerifiedStatusAsync(
                It.Is<IReadOnlyCollection<PackageRegistration>>(packages => packages.Count == 3
                    && packages.Select(package => package.Key).OrderBy(key => key).SequenceEqual(new[] { 10, 11, 12 })), true, false), Times.Once);
            Assert.Collection(fixture.Audits,
                audit => AssertAudit(audit, "Contoso.Tools", AuditedReservedNamespaceAction.ReserveNamespace),
                audit => AssertAudit(audit, "Contoso.Tools", AuditedReservedNamespaceAction.AddOwner, "fresh-submitter", "Contoso.Tools"),
                audit => AssertAudit(audit, "Contoso.Tools", AuditedReservedNamespaceAction.AddOwner, "fresh-organization"),
                audit => AssertAudit(audit, "Contoso.Tools.", AuditedReservedNamespaceAction.ReserveNamespace),
                audit => AssertAudit(audit, "Contoso.Tools.", AuditedReservedNamespaceAction.AddOwner, "fresh-submitter", "Contoso.Tools.Shared"),
                audit => AssertAudit(audit, "Contoso.Tools.", AuditedReservedNamespaceAction.AddOwner, "fresh-organization", "Contoso.Tools.Child", "Contoso.Tools.Shared"));
        }

        [Fact]
        public async Task EmptyPackageMatchesDoNotInvokeVerification()
        {
            var fixture = new Fixture();

            await fixture.AllocateAsync();

            fixture.AssertSuccess();
            fixture.Packages.Verify(service => service.UpdatePackageVerifiedStatusAsync(
                It.IsAny<IReadOnlyCollection<PackageRegistration>>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task UsesOriginalKeySetNotSavedUsernamesAndDoesNotAddSubmitterAsOwner()
        {
            var fixture = new Fixture();
            fixture.SavedRequest.RequestedOwnersJson = "[{\"Key\":2,\"Username\":\"untrusted-name\"},{\"Key\":2}]";
            fixture.FreshOrganization.EmailAddress = null; // Only the submitter must be confirmed.

            await fixture.AllocateAsync(new[] { 2, 2 });

            fixture.AssertSuccess(ownerCount: 1);
            Assert.All(fixture.Durable, allocation => Assert.Equal(new[] { 2 }, allocation.OwnerKeys));
            Assert.All(fixture.Audits.Where(audit => audit.Action == AuditedReservedNamespaceAction.AddOwner),
                audit => Assert.Equal("fresh-organization", audit.AffectedOwner));
        }

        [Fact]
        public async Task SelfOwnershipDoesNotRequireOrganizationMembership()
        {
            var fixture = new Fixture();
            fixture.SavedRequest.RequestedOwnersJson = "[{\"Key\":1}]";
            fixture.FreshMemberships.Remove(fixture.AdminMembership);

            await fixture.AllocateAsync(new[] { 1 });

            fixture.AssertSuccess(ownerCount: 1);
            Assert.All(fixture.Durable, allocation => Assert.Equal(new[] { 1 }, allocation.OwnerKeys));
        }

        [Fact]
        public async Task SnapshotsAndDeduplicatesOwnerCollectionBeforeDatabaseAccessAndFirstAwait()
        {
            var fixture = new Fixture();
            var ownerKeys = new List<int> { 2, 1, 2 };
            fixture.OnBegin = () =>
            {
                ownerKeys.Clear();
                ownerKeys.Add(3);
            };
            fixture.SaveGate = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var allocation = fixture.AllocateAsync(ownerKeys);
            Assert.False(allocation.IsCompleted);
            Assert.Equal(1, fixture.SaveAttempts);
            ownerKeys.Clear();
            ownerKeys.Add(-1);
            fixture.SaveGate.SetResult(null);
            await allocation;

            fixture.AssertSuccess();
            Assert.All(fixture.Durable, reservation => Assert.Equal(new[] { 1, 2 }, reservation.OwnerKeys));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(" Contoso.Tools")]
        [InlineData("Contoso.Tools ")]
        [InlineData("Contoso.Tools\n")]
        [InlineData("Contoso.*")]
        [InlineData("Contoso.Tools.")]
        [InlineData("Contoso..Tools")]
        [InlineData("Contoso-Tools-")]
        [InlineData("Contosó.Tools")]
        [InlineData("$id$")]
        public async Task InvalidBaseNamespaceFailsBeforeDatabaseAccess(string value)
        {
            var fixture = new Fixture();

            await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ReserveNamespaceForRequestAsync(7, value, 1, new[] { 1, 2 }));

            fixture.AssertNoDatabaseAccess();
        }

        [Theory]
        [InlineData(127, true)]
        [InlineData(128, false)]
        public async Task EnforcesLengthLeavingRoomForDottedPrefix(int length, bool valid)
        {
            var fixture = new Fixture(new string('a', length));
            if (valid)
            {
                await fixture.AllocateAsync();
                fixture.AssertSuccess();
                Assert.Equal(128, fixture.Durable.Single(allocation => allocation.IsPrefix).Value.Length);
            }
            else
            {
                await Assert.ThrowsAsync<ArgumentException>(() => fixture.AllocateAsync());
                fixture.AssertNoDatabaseAccess();
            }
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(7, 0)]
        [InlineData(7, -1)]
        public async Task NonpositiveRequestOrSubmitterKeyFailsBeforeDatabaseAccess(int requestKey, int submitterKey)
        {
            var fixture = new Fixture();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.ReserveNamespaceForRequestAsync(
                requestKey, fixture.NamespaceValue, submitterKey, new[] { 1, 2 }));

            fixture.AssertNoDatabaseAccess();
        }

        [Fact]
        public async Task NullOwnerKeysFailBeforeDatabaseAccess()
        {
            var fixture = new Fixture();

            await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.ReserveNamespaceForRequestAsync(7, fixture.NamespaceValue, 1, null));

            fixture.AssertNoDatabaseAccess();
        }

        [Theory]
        [InlineData(new int[0])]
        [InlineData(new[] { 0 })]
        [InlineData(new[] { -1 })]
        [InlineData(new[] { 1, 2, 0 })]
        public async Task EmptyOrNonpositiveOwnerKeysFailBeforeDatabaseAccess(int[] ownerKeys)
        {
            var fixture = new Fixture();

            await Assert.ThrowsAsync<ArgumentException>(() => fixture.AllocateAsync(ownerKeys));

            fixture.AssertNoDatabaseAccess();
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("different-key")]
        [InlineData("pending")]
        [InlineData("rejected")]
        [InlineData("reserved")]
        [InlineData("status-case")]
        [InlineData("no-completion")]
        [InlineData("different-submitter")]
        [InlineData("different-namespace")]
        [InlineData("namespace-case")]
        [InlineData("namespace-space")]
        [InlineData("missing-owner")]
        [InlineData("extra-owner")]
        public async Task RejectsChangedSavedApprovalEvenWhenTrackedApprovalStillMatches(string change)
        {
            var fixture = new Fixture();
            switch (change)
            {
                case "missing": fixture.FreshRequests.Remove(fixture.SavedRequest); break;
                case "different-key": fixture.SavedRequest.Key = 8; break;
                case "pending": fixture.SavedRequest.Status = "Pending"; break;
                case "rejected": fixture.SavedRequest.Status = "Rejected"; break;
                case "reserved": fixture.SavedRequest.Status = "Reserved"; break;
                case "status-case": fixture.SavedRequest.Status = "approved"; break;
                case "no-completion": fixture.SavedRequest.CompletedTimestamp = null; break;
                case "different-submitter": fixture.SavedRequest.SubmittedByUserKey = 3; break;
                case "different-namespace": fixture.SavedRequest.Namespace = "Other"; break;
                case "namespace-case": fixture.SavedRequest.Namespace = "contoso.tools"; break;
                case "namespace-space": fixture.SavedRequest.Namespace += " "; break;
                case "missing-owner": fixture.SavedRequest.RequestedOwnersJson = "[{\"Key\":1}]"; break;
                case "extra-owner": fixture.SavedRequest.RequestedOwnersJson = "[{\"Key\":1},{\"Key\":2},{\"Key\":3}]"; break;
                default: throw new ArgumentException(nameof(change));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            fixture.AssertRejectedBeforeStaging();
            fixture.VerifyFreshRequestRead();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("not json")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("[null]")]
        [InlineData("[1,2]")]
        [InlineData("[{}, {\"Key\":2}]")]
        [InlineData("[{\"Username\":\"fresh-submitter\"},{\"Key\":2}]")]
        [InlineData("[{\"key\":1},{\"Key\":2}]")]
        [InlineData("[{\"Key\":null},{\"Key\":2}]")]
        [InlineData("[{\"Key\":\"1\"},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1.0},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1.5},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1e0},{\"Key\":2}]")]
        [InlineData("[{\"Key\":true},{\"Key\":2}]")]
        [InlineData("[{\"Key\":[1]},{\"Key\":2}]")]
        [InlineData("[{\"Key\":0},{\"Key\":2}]")]
        [InlineData("[{\"Key\":-1},{\"Key\":2}]")]
        [InlineData("[{\"Key\":2147483648},{\"Key\":2}]")]
        [InlineData("[{\"Key\":999999999999999999999999999999},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1,\"Key\":1},{\"Key\":2}]")]
        [InlineData("[{\"Key\":3,\"Key\":1},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1,\"Username\":\"a\",\"Username\":\"b\"},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1,\"Extra\":{\"Key\":1,\"Key\":2}},{\"Key\":2}]")]
        [InlineData("[{\"Key\":1},{\"Key\":2},null]")]
        [InlineData("[{\"Key\":1},{\"Key\":2}] []")]
        public async Task SavedOwnerJsonFailsClosed(string json)
        {
            var fixture = new Fixture();
            fixture.SavedRequest.RequestedOwnersJson = json;

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            fixture.AssertRejectedBeforeStaging();
        }

        [Theory]
        [InlineData("missing-submitter")]
        [InlineData("unconfirmed-submitter")]
        [InlineData("locked-submitter")]
        [InlineData("deleted-submitter")]
        [InlineData("missing-owner")]
        [InlineData("locked-owner")]
        [InlineData("deleted-owner")]
        [InlineData("personal-owner")]
        [InlineData("membership-removed")]
        [InlineData("membership-not-admin")]
        [InlineData("membership-other-member")]
        [InlineData("membership-other-organization")]
        public async Task RejectsFreshAccountOrMembershipChangesDespiteStaleAuthorizedTrackedEntities(string change)
        {
            var fixture = new Fixture();
            switch (change)
            {
                case "missing-submitter": fixture.FreshUsers.Remove(fixture.FreshSubmitter); break;
                case "unconfirmed-submitter": fixture.FreshSubmitter.EmailAddress = null; break;
                case "locked-submitter": fixture.FreshSubmitter.UserStatusKey = UserStatus.Locked; break;
                case "deleted-submitter": fixture.FreshSubmitter.IsDeleted = true; break;
                case "missing-owner": fixture.FreshUsers.Remove(fixture.FreshOrganization); break;
                case "locked-owner": fixture.FreshOrganization.UserStatusKey = UserStatus.Locked; break;
                case "deleted-owner": fixture.FreshOrganization.IsDeleted = true; break;
                case "personal-owner":
                    fixture.FreshUsers.Remove(fixture.FreshOrganization);
                    fixture.FreshUsers.Add(new User("fresh-organization") { Key = 2 });
                    break;
                case "membership-removed": fixture.FreshMemberships.Remove(fixture.AdminMembership); break;
                case "membership-not-admin": fixture.AdminMembership.IsAdmin = false; break;
                case "membership-other-member": fixture.AdminMembership.MemberKey = 3; break;
                case "membership-other-organization": fixture.AdminMembership.OrganizationKey = 3; break;
                default: throw new ArgumentException(nameof(change));
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            fixture.AssertRejectedBeforeStaging();
            fixture.VerifyFreshAuthorizationReads();
        }

        [Fact]
        public async Task FreshAuthorizationCanSucceedDespiteStaleTrackedAccountState()
        {
            var fixture = new Fixture();
            foreach (var user in fixture.TrackedUsers)
            {
                user.IsDeleted = true;
                user.UserStatusKey = UserStatus.Locked;
                user.EmailAddress = null;
            }
            fixture.TrackedMemberships.Single().IsAdmin = false;
            fixture.TrackedRequest.Status = "Pending";
            fixture.TrackedRequest.CompletedTimestamp = null;

            await fixture.AllocateAsync();

            fixture.AssertSuccess();
        }

        [Fact]
        public async Task MissingTrackedOwnerFailsBeforeStaging()
        {
            var fixture = new Fixture();
            fixture.TrackedUsers.Remove(fixture.TrackedUsers.Single(user => user.Key == 2));

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            fixture.AssertRejectedBeforeStaging();
        }

        public static IEnumerable<object[]> Overlaps
        {
            get
            {
                var conflicts = new[]
                {
                    new ReservedNamespace("Contoso.Tools", false, false),
                    new ReservedNamespace("Contoso.Tools", false, true),
                    new ReservedNamespace("Contoso.Tools.", false, true),
                    new ReservedNamespace("Contoso.Tools.", false, false),
                    new ReservedNamespace("Contoso.Tools.Child", false, false),
                    new ReservedNamespace("Contoso.Tools.Child.", false, true),
                    new ReservedNamespace("Contoso.ToolsExtra", false, false),
                    new ReservedNamespace("Contoso.ToolsExtra", false, true),
                    new ReservedNamespace("Contoso.", false, true),
                    new ReservedNamespace("Contoso", false, true),
                    new ReservedNamespace("Con", false, true)
                };
                foreach (var conflict in conflicts)
                {
                    foreach (var shared in new[] { false, true })
                    {
                        foreach (var sameOwners in new[] { false, true })
                        {
                            yield return new object[] { conflict.Value, conflict.IsPrefix, shared, sameOwners };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(Overlaps))]
        public async Task RejectsEveryRelevantOverlapIncludingSharedAndSameOwner(string value, bool isPrefix, bool shared, bool sameOwners)
        {
            var fixture = new Fixture();
            var conflict = new ReservedNamespace(value, shared, isPrefix);
            foreach (var owner in fixture.TrackedUsers.Where(user => sameOwners ? user.Key <= 2 : user.Key == 3))
            {
                conflict.Owners.Add(owner);
            }
            fixture.Existing.Add(conflict);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            fixture.AssertRejectedBeforeStaging();
            fixture.Repository.Verify(repository => repository.GetAll(), Times.Exactly(2));
        }

        [Fact]
        public async Task UnrelatedReservationsAndExactOnlyParentDoNotConflict()
        {
            var fixture = new Fixture();
            fixture.Existing.Add(new ReservedNamespace("Contoso", false, false));
            fixture.Existing.Add(new ReservedNamespace("Contoso.Other.", true, true));

            await fixture.AllocateAsync();

            fixture.AssertSuccess();
        }

        [Theory]
        [InlineData("verification", 0, 0)]
        [InlineData("save", 1, 0)]
        [InlineData("transaction-commit", 1, 1)]
        public async Task PrecommitFailuresPropagateWithoutDurableAllocationAuditRetryOrCompensation(string failure, int saves, int commits)
        {
            var fixture = new Fixture();
            fixture.AddPackage(10, "Contoso.Tools", 1, 2);
            fixture.AddPackage(11, "Contoso.Tools.Child", 2);
            var error = new InvalidOperationException(failure);
            switch (failure)
            {
                case "verification": fixture.VerificationError = error; break;
                case "save": fixture.SaveError = error; break;
                case "transaction-commit": fixture.CommitError = error; break;
                default: throw new ArgumentException(nameof(failure));
            }

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            Assert.Same(error, actual);
            fixture.AssertRolledBackWithoutAudits();
            Assert.Equal(saves, fixture.SaveAttempts);
            Assert.Equal(commits, fixture.CommitAttempts);
            Assert.Equal(1, fixture.VerificationAttempts);
            fixture.Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<ReservedNamespace>()), Times.Exactly(2));
            fixture.Repository.Verify(repository => repository.CommitChangesAsync(), Times.Exactly(saves));
            fixture.Transaction.Verify(transaction => transaction.Commit(), Times.Exactly(commits));
            fixture.Repository.Verify(repository => repository.DeleteOnCommit(It.IsAny<ReservedNamespace>()), Times.Never);
            Assert.All(fixture.DurableVerification.Values, verified => Assert.False(verified));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        public async Task PostcommitAuditFailuresPropagateWithAllocationAlreadyDurable(int failAt)
        {
            var fixture = new Fixture();
            fixture.AddPackage(10, "Contoso.Tools", 1);
            var error = new InvalidOperationException("audit failed");
            fixture.AuditError = error;
            fixture.FailAuditAt = failAt;

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AllocateAsync());

            Assert.Same(error, actual);
            Assert.True(fixture.Committed);
            Assert.Equal(2, fixture.Durable.Count);
            Assert.True(fixture.DurableVerification[10]);
            Assert.Equal(failAt, fixture.AuditAttempts);
            Assert.Equal(failAt - 1, fixture.Audits.Count);
            Assert.Equal(1, fixture.SaveAttempts);
            Assert.Equal(1, fixture.CommitAttempts);
            fixture.Repository.Verify(repository => repository.CommitChangesAsync(), Times.Once);
            fixture.Transaction.Verify(transaction => transaction.Commit(), Times.Once);
            fixture.Transaction.Verify(transaction => transaction.Dispose(), Times.Once);
            fixture.Repository.Verify(repository => repository.DeleteOnCommit(It.IsAny<ReservedNamespace>()), Times.Never);
        }

        private static void AssertAudit(ReservedNamespaceAuditRecord audit, string value,
            AuditedReservedNamespaceAction action, string owner = null, params string[] packages)
        {
            Assert.Equal(value, audit.Value);
            Assert.Equal(action, audit.Action);
            Assert.Equal(owner, audit.AffectedOwner);
            Assert.False(audit.AffectedReservedNamespace.IsSharedNamespace);
            Assert.Equal(value.EndsWith(".", StringComparison.Ordinal), audit.AffectedReservedNamespace.IsPrefix);
            Assert.Equal(packages.OrderBy(id => id), (audit.AffectedRegistrations ?? new PackageRegistrationAuditRecord[0]).Select(package => package.Id).OrderBy(id => id));
            Assert.All(audit.AffectedRegistrations ?? new PackageRegistrationAuditRecord[0], package =>
            {
                Assert.Equal(AuditedPackageRegistrationAction.MarkVerified, package.Action);
                Assert.True(package.RegistrationRecord.IsVerified);
            });
        }

        // These strict dependency mocks exercise ordering and a fake rollback/durability boundary.
        // They do not emulate EF tracking rollback, SQL collation, locks, deadlocks or real concurrency.
        private sealed class Fixture
        {
            public Mock<IEntitiesContext> Context { get; } = new Mock<IEntitiesContext>(MockBehavior.Strict);
            public Mock<IDatabase> Database { get; } = new Mock<IDatabase>(MockBehavior.Strict);
            public Mock<IDbContextTransaction> Transaction { get; } = new Mock<IDbContextTransaction>(MockBehavior.Strict);
            public Mock<IEntityRepository<ReservedNamespace>> Repository { get; } = new Mock<IEntityRepository<ReservedNamespace>>(MockBehavior.Strict);
            public Mock<IUserService> Users { get; } = new Mock<IUserService>(MockBehavior.Strict);
            public Mock<IPackageService> Packages { get; } = new Mock<IPackageService>(MockBehavior.Strict);
            public Mock<IAuditingService> Auditing { get; } = new Mock<IAuditingService>(MockBehavior.Strict);
            public DbSet<NamespaceReservationRequest> TrackedRequests { get; } = FakeEntitiesContext.CreateDbSet<NamespaceReservationRequest>();
            public DbSet<NamespaceReservationRequest> FreshRequests { get; } = FakeEntitiesContext.CreateDbSet<NamespaceReservationRequest>();
            public DbSet<User> TrackedUsers { get; } = FakeEntitiesContext.CreateDbSet<User>();
            public DbSet<User> FreshUsers { get; } = FakeEntitiesContext.CreateDbSet<User>();
            public DbSet<Membership> TrackedMemberships { get; } = FakeEntitiesContext.CreateDbSet<Membership>();
            public DbSet<Membership> FreshMemberships { get; } = FakeEntitiesContext.CreateDbSet<Membership>();
            public List<ReservedNamespace> Existing { get; } = new List<ReservedNamespace>();
            public List<ReservedNamespace> Staged { get; } = new List<ReservedNamespace>();
            public List<AllocationSnapshot> Pending { get; } = new List<AllocationSnapshot>();
            public List<AllocationSnapshot> Durable { get; } = new List<AllocationSnapshot>();
            public List<PackageRegistration> Registrations { get; } = new List<PackageRegistration>();
            public Dictionary<int, bool> DurableVerification { get; } = new Dictionary<int, bool>();
            public List<ReservedNamespaceAuditRecord> Audits { get; } = new List<ReservedNamespaceAuditRecord>();
            public NamespaceReservationRequest SavedRequest { get; }
            public NamespaceReservationRequest TrackedRequest { get; }
            public User FreshSubmitter { get; } = new User("fresh-submitter") { Key = 1, EmailAddress = "submitter@example.test" };
            public Organization FreshOrganization { get; } = new Organization { Key = 2, Username = "fresh-organization" };
            public Membership AdminMembership { get; } = new Membership { OrganizationKey = 2, MemberKey = 1, IsAdmin = true };
            public ReservedNamespaceService Service { get; }
            public string NamespaceValue { get; }
            public Action BeforeSave { get; set; }
            public Action OnBegin { get; set; }
            public TaskCompletionSource<object> SaveGate { get; set; }
            public Exception VerificationError { get; set; }
            public Exception SaveError { get; set; }
            public Exception CommitError { get; set; }
            public Exception AuditError { get; set; }
            public int FailAuditAt { get; set; }
            public int SaveAttempts { get; private set; }
            public int CommitAttempts { get; private set; }
            public int VerificationAttempts { get; private set; }
            public int AuditAttempts { get; private set; }
            public bool Committed { get; private set; }
            private bool _inTransaction;
            private Dictionary<int, bool> _pendingVerification;

            public Fixture(string namespaceValue = "Contoso.Tools")
            {
                NamespaceValue = namespaceValue;
                SavedRequest = new NamespaceReservationRequest
                {
                    Key = 7,
                    Namespace = namespaceValue,
                    SubmittedByUserKey = 1,
                    RequestedOwnersJson = "[{\"Key\":1,\"Username\":\"old-submitter\"},{\"Key\":2,\"Username\":\"old-organization\"}]",
                    Status = "Approved",
                    Reason = "Assessment approved with evidence.",
                    CreatedTimestamp = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    CompletedTimestamp = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
                    Justification = "Original justification."
                };
                TrackedRequest = JsonConvert.DeserializeObject<NamespaceReservationRequest>(JsonConvert.SerializeObject(SavedRequest));
                FreshRequests.Add(SavedRequest);
                TrackedRequests.Add(TrackedRequest);
                FreshUsers.Add(FreshSubmitter);
                FreshUsers.Add(FreshOrganization);
                TrackedUsers.Add(new User("stale-submitter") { Key = 1, EmailAddress = "stale@example.test" });
                TrackedUsers.Add(new Organization { Key = 2, Username = "stale-organization" });
                TrackedUsers.Add(new User("not-authorized") { Key = 3 });
                FreshMemberships.Add(AdminMembership);
                TrackedMemberships.Add(new Membership { OrganizationKey = 2, MemberKey = 1, IsAdmin = true });
                Mock.Get(TrackedRequests).Setup(set => set.AsNoTracking()).Callback(CheckRead).Returns(FreshRequests);
                Mock.Get(TrackedUsers).Setup(set => set.AsNoTracking()).Callback(CheckRead).Returns(FreshUsers);
                Mock.Get(TrackedMemberships).Setup(set => set.AsNoTracking()).Callback(CheckRead).Returns(FreshMemberships);
                Context.Setup(context => context.Set<NamespaceReservationRequest>()).Callback(CheckRead).Returns(TrackedRequests);
                Context.Setup(context => context.Set<Membership>()).Callback(CheckRead).Returns(TrackedMemberships);
                Context.Setup(context => context.Users).Callback(CheckRead).Returns(TrackedUsers);
                Context.Setup(context => context.GetDatabase()).Returns(Database.Object);
                Database.Setup(database => database.BeginTransaction(IsolationLevel.Serializable)).Returns(() =>
                {
                    Assert.True(EntitiesConfiguration.SuspendExecutionStrategy);
                    Assert.False(_inTransaction);
                    _inTransaction = true;
                    OnBegin?.Invoke();
                    return Transaction.Object;
                });
                Repository.Setup(repository => repository.GetAll()).Callback(CheckRead).Returns(() => Existing.AsQueryable());
                Repository.Setup(repository => repository.InsertOnCommit(It.IsAny<ReservedNamespace>())).Callback<ReservedNamespace>(allocation =>
                {
                    Assert.True(_inTransaction);
                    Assert.False(Committed);
                    VerifyFreshAuthorizationReads();
                    Staged.Add(allocation);
                });
                Packages.Setup(service => service.FindPackageRegistrationsByOwner(It.IsAny<User>())).Returns((User owner) =>
                {
                    Assert.True(_inTransaction);
                    Assert.Same(TrackedUsers.Single(user => user.Key == owner.Key), owner);
                    return Registrations.Where(package => package.Owners.Any(user => user.Key == owner.Key)).AsQueryable();
                });
                Packages.Setup(service => service.UpdatePackageVerifiedStatusAsync(It.IsAny<IReadOnlyCollection<PackageRegistration>>(), true, false))
                    .Returns((IReadOnlyCollection<PackageRegistration> packages, bool verified, bool commitChanges) =>
                    {
                        Assert.True(_inTransaction);
                        Assert.False(Committed);
                        Assert.Equal(0, SaveAttempts);
                        VerificationAttempts++;
                        foreach (var package in packages)
                        {
                            package.IsVerified = verified;
                        }
                        return VerificationError == null ? Task.CompletedTask : Task.FromException(VerificationError);
                    });
                Repository.Setup(repository => repository.CommitChangesAsync()).Returns(() =>
                {
                    Assert.True(_inTransaction);
                    Assert.True(EntitiesConfiguration.SuspendExecutionStrategy);
                    Assert.False(Committed);
                    Assert.Equal(0, SaveAttempts++);
                    Assert.Equal(2, Staged.Count);
                    Assert.Empty(Audits);
                    BeforeSave?.Invoke();
                    Pending.AddRange(Staged.Select(allocation => new AllocationSnapshot(allocation)));
                    _pendingVerification = Registrations.GroupBy(package => package.Key).ToDictionary(group => group.Key, group => group.First().IsVerified);
                    return SaveError == null ? SaveGate?.Task ?? Task.CompletedTask : Task.FromException(SaveError);
                });
                Transaction.Setup(transaction => transaction.Commit()).Callback(() =>
                {
                    Assert.True(_inTransaction);
                    Assert.True(EntitiesConfiguration.SuspendExecutionStrategy);
                    Assert.Equal(1, SaveAttempts);
                    Assert.Equal(0, CommitAttempts++);
                    Assert.Equal(2, Pending.Count);
                    Assert.Empty(Audits);
                    if (CommitError != null)
                    {
                        throw CommitError;
                    }
                    Durable.AddRange(Pending);
                    foreach (var package in _pendingVerification)
                    {
                        DurableVerification[package.Key] = package.Value;
                    }
                    Committed = true;
                });
                Transaction.Setup(transaction => transaction.Dispose()).Callback(() =>
                {
                    Assert.True(_inTransaction);
                    _inTransaction = false;
                    Pending.Clear(); // Fake rollback: intentionally does not undo tracked in-memory state.
                    _pendingVerification = null;
                });
                Auditing.Setup(service => service.SaveAuditRecordAsync(It.IsAny<AuditRecord>())).Returns((AuditRecord record) =>
                {
                    Assert.True(Committed);
                    Assert.Equal(2, Durable.Count);
                    Assert.Equal(1, CommitAttempts);
                    AuditAttempts++;
                    if (AuditError != null && AuditAttempts == FailAuditAt)
                    {
                        return Task.FromException(AuditError);
                    }
                    Audits.Add(Assert.IsType<ReservedNamespaceAuditRecord>(record));
                    return Task.CompletedTask;
                });
                // Seed calls belong to Arrange, not database access by the allocator.
                // Moq also verifies these returned DbSet mocks recursively.
                Mock.Get(FreshRequests).Invocations.Clear();
                Mock.Get(TrackedRequests).Invocations.Clear();
                Mock.Get(FreshUsers).Invocations.Clear();
                Mock.Get(TrackedUsers).Invocations.Clear();
                Mock.Get(FreshMemberships).Invocations.Clear();
                Mock.Get(TrackedMemberships).Invocations.Clear();
                Service = new ReservedNamespaceService(Context.Object, Repository.Object, Users.Object, Packages.Object, Auditing.Object);
            }

            public async Task AllocateAsync(IReadOnlyCollection<int> ownerKeys = null)
            {
                var savedBefore = JsonConvert.SerializeObject(SavedRequest);
                var trackedBefore = JsonConvert.SerializeObject(TrackedRequest);
                try
                {
                    await Service.ReserveNamespaceForRequestAsync(7, NamespaceValue, 1, ownerKeys ?? new[] { 1, 2 });
                }
                finally
                {
                    // Includes Status, Reason, completion, original scope and all other assessment fields.
                    Assert.Equal(savedBefore, JsonConvert.SerializeObject(SavedRequest));
                    Assert.Equal(trackedBefore, JsonConvert.SerializeObject(TrackedRequest));
                }
            }

            public PackageRegistration AddPackage(int key, string id, params int[] ownerKeys)
            {
                var package = new PackageRegistration { Key = key, Id = id };
                foreach (var ownerKey in ownerKeys)
                {
                    package.Owners.Add(TrackedUsers.Single(user => user.Key == ownerKey));
                }
                Registrations.Add(package);
                DurableVerification.Add(key, package.IsVerified);
                return package;
            }

            private void CheckRead()
            {
                Assert.True(_inTransaction);
                Assert.True(EntitiesConfiguration.SuspendExecutionStrategy);
                Assert.Empty(Staged);
                Assert.Equal(0, SaveAttempts);
            }

            public void VerifyFreshRequestRead()
            {
                Mock.Get(TrackedRequests).Verify(set => set.AsNoTracking(), Times.Once);
            }

            public void VerifyFreshAuthorizationReads()
            {
                VerifyFreshRequestRead();
                Mock.Get(TrackedUsers).Verify(set => set.AsNoTracking(), Times.Once);
                Mock.Get(TrackedMemberships).Verify(set => set.AsNoTracking(), Times.Once);
            }

            public void AssertSuccess(int ownerCount = 2)
            {
                Assert.True(Committed);
                Assert.False(_inTransaction);
                Assert.Equal(2, Durable.Count);
                Assert.Equal(1, SaveAttempts);
                Assert.Equal(1, CommitAttempts);
                Assert.Equal(2 * (1 + ownerCount), Audits.Count);
                Assert.All(Durable, allocation => Assert.False(allocation.IsShared));
                Assert.All(Durable, allocation => Assert.Equal(ownerCount, allocation.OwnerKeys.Length));
                Database.Verify(database => database.BeginTransaction(IsolationLevel.Serializable), Times.Once);
                Database.VerifyNoOtherCalls();
                Repository.Verify(repository => repository.GetAll(), Times.Exactly(2));
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<ReservedNamespace>()), Times.Exactly(2));
                Repository.Verify(repository => repository.CommitChangesAsync(), Times.Once);
                Repository.VerifyNoOtherCalls();
                Transaction.Verify(transaction => transaction.Commit(), Times.Once);
                Transaction.Verify(transaction => transaction.Dispose(), Times.Once);
                Transaction.VerifyNoOtherCalls();
                Users.VerifyNoOtherCalls();
                VerifyFreshAuthorizationReads();
            }

            public void AssertNoDatabaseAccess()
            {
                Context.VerifyNoOtherCalls();
                Database.VerifyNoOtherCalls();
                Repository.VerifyNoOtherCalls();
                Packages.VerifyNoOtherCalls();
                Auditing.VerifyNoOtherCalls();
                Users.VerifyNoOtherCalls();
            }

            public void AssertRejectedBeforeStaging()
            {
                Assert.Empty(Staged);
                Assert.Equal(0, SaveAttempts);
                Assert.Equal(0, CommitAttempts);
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<ReservedNamespace>()), Times.Never);
                Repository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
                Packages.VerifyNoOtherCalls();
                Users.VerifyNoOtherCalls();
                AssertRolledBackWithoutAudits();
            }

            public void AssertRolledBackWithoutAudits()
            {
                Assert.False(Committed);
                Assert.False(_inTransaction);
                Assert.Empty(Durable);
                Assert.Empty(Pending);
                Assert.Empty(Audits);
                Assert.Equal(0, AuditAttempts);
                Auditing.VerifyNoOtherCalls();
                Database.Verify(database => database.BeginTransaction(IsolationLevel.Serializable), Times.Once);
                Database.VerifyNoOtherCalls();
                Transaction.Verify(transaction => transaction.Dispose(), Times.Once);
            }
        }

        private sealed class AllocationSnapshot
        {
            public string Value { get; }
            public bool IsPrefix { get; }
            public bool IsShared { get; }
            public int[] OwnerKeys { get; }
            public int[] PackageKeys { get; }

            public AllocationSnapshot(ReservedNamespace allocation)
            {
                Value = allocation.Value;
                IsPrefix = allocation.IsPrefix;
                IsShared = allocation.IsSharedNamespace;
                OwnerKeys = allocation.Owners.Select(owner => owner.Key).OrderBy(key => key).ToArray();
                PackageKeys = allocation.PackageRegistrations.Select(package => package.Key).OrderBy(key => key).ToArray();
            }
        }
    }
}