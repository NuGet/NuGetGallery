// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Services
{
    public class NamespaceReservationRequestServiceFacts
    {
        public static IEnumerable<object[]> InvalidFieldValues
        {
            get
            {
                foreach (var field in FieldLengths)
                {
                    yield return new object[] { field.Key, null };
                    yield return new object[] { field.Key, string.Empty };
                    yield return new object[] { field.Key, " \t\r\n " };
                    yield return new object[] { field.Key, new string('a', field.Value + 1) };
                    // Limits apply to the original input, before trimming.
                    yield return new object[] { field.Key, " " + new string('a', field.Value - 1) + " " };
                }
            }
        }

        public static IEnumerable<object[]> ValidFieldLengths
        {
            get
            {
                foreach (var field in FieldLengths)
                {
                    yield return new object[] { field.Key, 1 };
                    yield return new object[] { field.Key, field.Value - 1 };
                    yield return new object[] { field.Key, field.Value };
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, int>> FieldLengths => new[]
        {
            new KeyValuePair<string, int>(nameof(NamespaceReservationRequestInput.Namespace), 127),
            new KeyValuePair<string, int>(nameof(NamespaceReservationRequestInput.Owner), 1000),
            new KeyValuePair<string, int>(nameof(NamespaceReservationRequestInput.Justification), 4000)
        };

        public class TheConstructor
        {
            [Theory]
            [InlineData("repository")]
            [InlineData("userService")]
            [InlineData("foundryClient")]
            [InlineData("telemetryService")]
            [InlineData("evidenceFactory")]
            [InlineData("reservedNamespaceService")]
            public void RejectsNullDependencies(string parameterName)
            {
                var lazy = new Lazy<INamespaceReservationFoundryClient>(() => throw new InvalidOperationException("Must not resolve during construction."));
                var exception = Assert.Throws<ArgumentNullException>(() => new NamespaceReservationRequestService(
                    parameterName == "repository" ? null : Mock.Of<IEntityRepository<NamespaceReservationRequest>>(),
                    parameterName == "userService" ? null : Mock.Of<IUserService>(),
                    parameterName == "foundryClient" ? null : lazy,
                    parameterName == "telemetryService" ? null : Mock.Of<ITelemetryService>(),
                    parameterName == "evidenceFactory" ? null : Mock.Of<INamespaceReservationEvidenceFactory>(),
                    parameterName == "reservedNamespaceService" ? null : Mock.Of<IReservedNamespaceService>()));

                Assert.Equal(parameterName, exception.ParamName);
                Assert.False(lazy.IsValueCreated);
            }
        }

        public class TheGetRequestsForUserMethod
        {
            [Fact]
            public void RejectsNullWithoutQuerying()
            {
                var fixture = new Fixture();

                Assert.Equal("user", Assert.Throws<ArgumentNullException>(() => fixture.Target.GetRequestsForUser(null)).ParamName);

                fixture.Repository.Verify(repository => repository.GetAll(), Times.Never);
            }

            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            [InlineData(int.MinValue)]
            public void RejectsNonpositiveKeysWithoutQuerying(int key)
            {
                var fixture = new Fixture();

                Assert.Equal("user", Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Target.GetRequestsForUser(new User { Key = key })).ParamName);

                fixture.Repository.Verify(repository => repository.GetAll(), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void FiltersOnQueryableAndMaterializesOnlySubmittersRequestsOldestFirst(bool isAdmin)
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin);
                var now = DateTime.UtcNow;
                var requests = new List<NamespaceReservationRequest>
                {
                    new NamespaceReservationRequest { Key = 9, SubmittedByUserKey = 42, CreatedTimestamp = now.AddDays(-1) },
                    new NamespaceReservationRequest { Key = 2, SubmittedByUserKey = 42, CreatedTimestamp = now },
                    new NamespaceReservationRequest { Key = 3, SubmittedByUserKey = 42, CreatedTimestamp = now },
                    new NamespaceReservationRequest { Key = 4, SubmittedByUserKey = organization.Key, CreatedTimestamp = now.AddDays(1) },
                    new NamespaceReservationRequest
                    {
                        Key = 5,
                        SubmittedByUserKey = 99,
                        CreatedTimestamp = now.AddDays(1),
                        RequestedOwnersJson = "[{\"Key\":100,\"Username\":\"Contoso\"}]"
                    }
                };
                var source = requests.AsQueryable();
                var provider = new Mock<IQueryProvider>(MockBehavior.Strict);
                provider.Setup(value => value.CreateQuery<NamespaceReservationRequest>(It.IsAny<Expression>()))
                    .Callback<Expression>(expression =>
                    {
                        var method = Assert.IsAssignableFrom<MethodCallExpression>(expression);
                        Assert.Equal(typeof(Queryable), method.Method.DeclaringType);
                        Assert.Equal(nameof(Queryable.Where), method.Method.Name);
                    })
                    .Returns<Expression>(expression => source.Provider.CreateQuery<NamespaceReservationRequest>(expression));
                var query = new Mock<IQueryable<NamespaceReservationRequest>>(MockBehavior.Strict);
                query.SetupGet(value => value.Provider).Returns(provider.Object);
                query.SetupGet(value => value.Expression).Returns(source.Expression);
                fixture.Repository.Setup(repository => repository.GetAll()).Returns(query.Object);

                IReadOnlyList<NamespaceReservationRequest> result = fixture.Target.GetRequestsForUser(fixture.Submitter);

                Assert.Equal(new[] { 9, 2, 3 }, result.Select(request => request.Key));
                Assert.All(result, request => Assert.Equal(fixture.Submitter.Key, request.SubmittedByUserKey));
                requests.Clear();
                Assert.Equal(3, result.Count);
                fixture.Repository.Verify(repository => repository.GetAll(), Times.Once);
                query.Verify(value => value.GetEnumerator(), Times.Never);
                fixture.VerifyNoWrites();
                fixture.VerifyNoOwnerLookups();
                fixture.VerifyNoAssessment();
                Assert.False(fixture.LazyFoundry.IsValueCreated);
            }

            [Fact]
            public void ReturnsEmptyListWhenUserHasNoRequests()
            {
                var fixture = new Fixture();
                fixture.Repository.Setup(repository => repository.GetAll()).Returns(new[]
                {
                    new NamespaceReservationRequest { SubmittedByUserKey = 99 }
                }.AsQueryable());

                Assert.Empty(fixture.Target.GetRequestsForUser(fixture.Submitter));
            }
        }

        public class TheInputClass
        {
            [Fact]
            public void DefinesExpectedMaximumLengths()
            {
                Assert.Equal(127, NamespaceReservationRequestInput.MaxNamespaceLength);
                Assert.Equal(1000, NamespaceReservationRequestInput.MaxOwnerLength);
                Assert.Equal(4000, NamespaceReservationRequestInput.MaxJustificationLength);
            }

            [Theory]
            [MemberData(nameof(InvalidFieldValues), MemberType = typeof(NamespaceReservationRequestServiceFacts))]
            public void RejectsInvalidFieldWithItsMemberName(string field, string value)
            {
                // Arrange
                var input = CreateInput();
                SetField(input, field, value);
                var errors = new List<ValidationResult>();

                // Act
                var valid = Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true);

                // Assert
                Assert.False(valid);
                AssertFieldError(errors, field);
            }

            [Theory]
            [MemberData(nameof(ValidFieldLengths), MemberType = typeof(NamespaceReservationRequestServiceFacts))]
            public void AcceptsFieldLengthBoundaries(string field, int length)
            {
                var input = CreateInput();
                SetField(input, field, new string('a', length));
                var errors = new List<ValidationResult>();

                var valid = Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true);

                Assert.True(valid);
                Assert.Empty(errors);
            }

            [Fact]
            public void ReportsAllMissingFields()
            {
                var input = new NamespaceReservationRequestInput();
                var errors = new List<ValidationResult>();

                Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true));

                Assert.Equal(3, errors.Count);
                Assert.Equal(
                    FieldLengths.Select(field => field.Key).OrderBy(name => name),
                    errors.Select(error => Assert.Single(error.MemberNames)).OrderBy(name => name));
            }
        }

        public class TheSubmitAsyncMethod
        {
            [Theory]
            [InlineData(true, "submitter")]
            [InlineData(false, "input")]
            public async Task RejectsNullArgumentsWithoutWriting(bool nullSubmitter, string parameterName)
            {
                var fixture = new Fixture();

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Target.SubmitAsync(
                    nullSubmitter ? null : fixture.Submitter,
                    nullSubmitter ? fixture.Input : null));

                Assert.Equal(parameterName, exception.ParamName);
                fixture.VerifyNoWrites();
                fixture.VerifyNoOwnerLookups();
            }

            [Fact]
            public async Task InsertsInitialPendingRowAndPersistsDisabledReasonInSecondCommit()
            {
                // Arrange
                var fixture = new Fixture();
                var before = DateTime.UtcNow;

                // Act
                IReadOnlyCollection<ValidationResult> errors = await fixture.SubmitAsync();
                var after = DateTime.UtcNow;

                // Assert
                Assert.Empty(errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal(7, request.Key);
                Assert.Equal(42, request.SubmittedByUserKey);
                Assert.Null(request.SubmittedByUser);
                Assert.Equal("Contoso.Tools", request.Namespace);
                Assert.Equal("We publish the Contoso tools.", request.Justification);
                Assert.Equal("Pending", request.Status);
                Assert.Equal("Model assessment is disabled.", request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Equal(DateTimeKind.Utc, request.CreatedTimestamp.Kind);
                Assert.InRange(request.CreatedTimestamp, before, after);
                AssertOwners(request, fixture.Submitter);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                var initial = fixture.CommittedSnapshots[0];
                Assert.Equal("Pending", initial.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, initial.Reason);
                Assert.Null(initial.CompletedTimestamp);
                Assert.Equal(request.CreatedTimestamp, initial.CreatedTimestamp);
                Assert.Equal(request.Reason, fixture.CommittedSnapshots[1].Reason);
                Assert.True(fixture.LazyFoundry.IsValueCreated);
                fixture.Foundry.VerifyGet(client => client.IsEnabled, Times.Once);
                fixture.VerifyNoAssessment();
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task TrimsFieldsAndStoresCanonicalOwnerNameWithoutChangingInput()
            {
                var fixture = new Fixture();
                fixture.Input.Namespace = " \tContoso.Tools-CLI\r\n ";
                fixture.Input.Owner = " \talice\r\n ";
                fixture.Input.Justification = " \tWe publish tools.\r\n ";

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Contoso.Tools-CLI", request.Namespace);
                Assert.Equal("We publish tools.", request.Justification);
                AssertOwners(request, fixture.Submitter);
                fixture.UserService.Verify(service => service.FindByUsername("alice", false), Times.Once);
                Assert.Equal(" \tContoso.Tools-CLI\r\n ", fixture.Input.Namespace);
                Assert.Equal(" \talice\r\n ", fixture.Input.Owner);
                Assert.Equal(" \tWe publish tools.\r\n ", fixture.Input.Justification);
            }

            [Fact]
            public async Task AllowsSelfAndMultipleAdministeredOrganizationsAndDeduplicatesNamesIgnoringCase()
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin: true);
                var otherOrganization = fixture.AddOrganization(101, "ContosoTools", isAdmin: true);
                fixture.Input.Owner = " alice, CONTOSO, Alice, contoso, ContosoTools, CONTOSOTOOLS ";

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                AssertOwners(fixture.VerifySingleWrite(), fixture.Submitter, organization, otherOrganization);
                fixture.UserService.Verify(service => service.FindByUsername("alice", false), Times.Once);
                fixture.UserService.Verify(service => service.FindByUsername("CONTOSO", false), Times.Once);
                fixture.UserService.Verify(service => service.FindByUsername("ContosoTools", false), Times.Once);
                fixture.UserService.Verify(service => service.FindByUsername(It.IsAny<string>(), false), Times.Exactly(3));
            }

            [Fact]
            public async Task DeduplicatesResolvedOwnersByKey()
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin: true);
                // Distinct lookup names can resolve to the same persisted account.
                fixture.RegisterOwner("Alias", organization);
                fixture.Input.Owner = "Contoso, Alias";

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                AssertOwners(fixture.VerifySingleWrite(), organization);
                fixture.UserService.Verify(service => service.FindByUsername("Contoso", false), Times.Once);
                fixture.UserService.Verify(service => service.FindByUsername("Alias", false), Times.Once);
            }

            [Fact]
            public async Task AllowsOrganizationAdminWithoutRequiringSelfAsOwner()
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin: true);
                fixture.Input.Owner = organization.Username;

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                AssertOwners(fixture.VerifySingleWrite(), organization);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task RejectsOrganizationCollaboratorEvenWithAnAuthorizedOwner(bool includeSelf)
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin: false);
                fixture.Input.Owner = (includeSelf ? "Alice, " : string.Empty) + organization.Username;

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Owner));
                fixture.VerifyNoWrites();
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task RejectsUnrelatedAccountEvenWithAnAuthorizedOwner(bool isOrganization, bool includeSelf)
            {
                var fixture = new Fixture();
                User unrelated = isOrganization ? new Organization("Unrelated") : new User("Unrelated");
                unrelated.Key = 100;
                unrelated.EmailAddress = "unrelated@example.test";
                fixture.RegisterOwner(unrelated.Username, unrelated);
                fixture.Input.Owner = (includeSelf ? "Alice, " : string.Empty) + unrelated.Username;

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Owner));
                fixture.VerifyNoWrites();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task RejectsNonexistentOwnerEvenWithAnAuthorizedOwner(bool includeSelf)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Must not assess invalid owners."));
                fixture.Input.Owner = (includeSelf ? "Alice, " : string.Empty) + "Missing";

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Owner));
                fixture.UserService.Verify(service => service.FindByUsername("Missing", false), Times.Once);
                fixture.VerifyNoWrites();
            }

            [Theory]
            [InlineData(false, true, false)]
            [InlineData(false, false, true)]
            [InlineData(false, true, true)]
            [InlineData(true, true, false)]
            [InlineData(true, false, true)]
            [InlineData(true, true, true)]
            public async Task RejectsDeletedOrLockedOwner(bool isOrganization, bool isDeleted, bool isLocked)
            {
                var fixture = new Fixture();
                User owner;
                if (isOrganization)
                {
                    owner = fixture.AddOrganization(100, "Contoso", isAdmin: true);
                    fixture.Input.Owner = "Alice, Contoso";
                }
                else
                {
                    // A separate entity for self keeps the submitter eligible, isolating owner validation.
                    owner = new User(fixture.Submitter.Username) { Key = fixture.Submitter.Key };
                    fixture.RegisterOwner(owner.Username, owner);
                }

                owner.IsDeleted = isDeleted;
                if (isLocked)
                {
                    owner.UserStatusKey = UserStatus.Locked;
                }

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Owner));
                fixture.VerifyNoWrites();
            }

            [Theory]
            [InlineData(false, false, false)]
            [InlineData(true, true, false)]
            [InlineData(true, false, true)]
            [InlineData(true, true, true)]
            [InlineData(false, true, false)]
            [InlineData(false, false, true)]
            [InlineData(false, true, true)]
            public async Task RejectsIneligibleSubmitterBeforeLookingUpOwners(bool confirmed, bool isDeleted, bool isLocked)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Must not assess ineligible submitters."));
                fixture.Submitter.EmailAddress = confirmed ? "alice@example.test" : null;
                fixture.Submitter.UnconfirmedEmailAddress = "pending@example.test";
                fixture.Submitter.IsDeleted = isDeleted;
                if (isLocked)
                {
                    fixture.Submitter.UserStatusKey = UserStatus.Locked;
                }

                var errors = await fixture.SubmitAsync();

                Assert.Equal((confirmed ? 0 : 1) + (isDeleted || isLocked ? 1 : 0), errors.Count);
                Assert.All(errors, error =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
                    Assert.Empty(error.MemberNames);
                });
                fixture.VerifyNoWrites();
                fixture.VerifyNoOwnerLookups();
            }

            [Theory]
            [MemberData(nameof(InvalidFieldValues), MemberType = typeof(NamespaceReservationRequestServiceFacts))]
            public async Task RejectsInvalidFieldWithoutWritingOrLookingUpOwners(string field, string value)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Must not assess invalid fields."));
                SetField(fixture.Input, field, value);

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, field);
                fixture.VerifyNoWrites();
                fixture.VerifyNoOwnerLookups();
            }

            [Theory]
            [MemberData(nameof(ValidFieldLengths), MemberType = typeof(NamespaceReservationRequestServiceFacts))]
            public async Task AcceptsFieldLengthBoundaries(string field, int length)
            {
                var fixture = new Fixture();
                var value = new string('a', length);
                if (field == nameof(NamespaceReservationRequestInput.Owner))
                {
                    fixture.Submitter.Username = "A";
                    fixture.RegisterOwner("A", fixture.Submitter);
                    value = "A".PadRight(length);
                }

                SetField(fixture.Input, field, value);

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal(fixture.Input.Namespace.Trim(), request.Namespace);
                Assert.Equal(fixture.Input.Justification.Trim(), request.Justification);
                AssertOwners(request, fixture.Submitter);
            }

            [Theory]
            [InlineData("*")]
            [InlineData("Contoso.*")]
            [InlineData("Contoso*")]
            [InlineData("Contoso.")]
            [InlineData(".Contoso")]
            [InlineData("Contoso..Tools")]
            [InlineData("Contoso--Tools")]
            [InlineData("Contoso.-Tools")]
            [InlineData("Contoso-")]
            [InlineData("-Contoso")]
            [InlineData("Contoso Tools")]
            [InlineData("Contoso/Tools")]
            [InlineData("Contoso\tTools")]
            [InlineData("Contosö")]
            public async Task RejectsInvalidNamespaceSyntax(string namespaceValue)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Must not assess invalid namespaces."));
                fixture.Input.Namespace = namespaceValue;

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Namespace));
                fixture.VerifyNoWrites();
            }

            [Theory]
            [InlineData("A")]
            [InlineData("Contoso")]
            [InlineData("Contoso.Tools")]
            [InlineData("Contoso-Tools")]
            [InlineData("Contoso.Tools-CLI")]
            [InlineData("Contoso_Tools.V2-CLI")]
            [InlineData("123")]
            [InlineData("_")]
            public async Task AcceptsValidNamespaceSyntax(string namespaceValue)
            {
                var fixture = new Fixture();
                fixture.Input.Namespace = namespaceValue;

                var errors = await fixture.SubmitAsync();

                Assert.Empty(errors);
                Assert.Equal(namespaceValue, fixture.VerifySingleWrite().Namespace);
            }

            [Theory]
            [InlineData(",")]
            [InlineData(",Alice")]
            [InlineData("Alice,")]
            [InlineData("Alice,,Alice")]
            [InlineData("Alice, ,Alice")]
            [InlineData("Alice,\t\r\n,Alice")]
            public async Task RejectsEmptyOwnerSegmentsWithoutLookingUpOwners(string owner)
            {
                var fixture = new Fixture();
                fixture.Input.Owner = owner;

                var errors = await fixture.SubmitAsync();

                AssertFieldError(errors, nameof(NamespaceReservationRequestInput.Owner));
                fixture.VerifyNoWrites();
                fixture.VerifyNoOwnerLookups();
            }

            [Fact]
            public async Task ReportsBothNamespaceAndOwnerErrorsWithoutWriting()
            {
                var fixture = new Fixture();
                fixture.Input.Namespace = "Contoso.*";
                fixture.Input.Owner = "Missing";

                var errors = await fixture.SubmitAsync();

                Assert.Equal(2, errors.Count);
                Assert.Contains(errors, error => error.MemberNames.SequenceEqual(new[] { nameof(NamespaceReservationRequestInput.Namespace) }));
                Assert.Contains(errors, error => error.MemberNames.SequenceEqual(new[] { nameof(NamespaceReservationRequestInput.Owner) }));
                fixture.VerifyNoWrites();
            }

            [Fact]
            public async Task ReportsEachUnavailableOwnerWithoutWriting()
            {
                var fixture = new Fixture();
                fixture.Input.Owner = "Missing, AlsoMissing";

                var errors = await fixture.SubmitAsync();

                Assert.Equal(2, errors.Count);
                Assert.All(errors, error => Assert.Equal(new[] { nameof(NamespaceReservationRequestInput.Owner) }, error.MemberNames));
                fixture.UserService.Verify(service => service.FindByUsername("Missing", false), Times.Once);
                fixture.UserService.Verify(service => service.FindByUsername("AlsoMissing", false), Times.Once);
                fixture.VerifyNoWrites();
            }

            [Fact]
            public async Task PropagatesDatabaseCommitException()
            {
                var fixture = new Fixture();
                var expected = new DbUpdateException("Database write failed.");
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).ThrowsAsync(expected);

                var actual = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.SubmitAsync());

                Assert.Same(expected, actual);
                var request = fixture.VerifySingleWrite(expectedCommits: 1);
                Assert.Equal("Pending", request.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, request.Reason);
                Assert.Null(request.CompletedTimestamp);
                fixture.VerifyNoResolution();
            }

            [Fact]
            public async Task PropagatesInsertExceptionWithoutCommitting()
            {
                var fixture = new Fixture();
                var expected = new InvalidOperationException("Insert failed.");
                fixture.Repository.Setup(repository => repository.InsertOnCommit(It.IsAny<NamespaceReservationRequest>())).Throws(expected);

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SubmitAsync());

                Assert.Same(expected, actual);
                fixture.Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()), Times.Once);
                fixture.Repository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
                fixture.VerifyNoResolution();
            }
        }

        public class TheAssessmentIntegration
        {
            [Fact]
            public async Task ApprovalIsSavedWithCompletionTimestampBeforeAllocationWithoutChangingToReserved()
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Criteria met."));
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(() =>
                {
                    if (fixture.Inserted.Status == "Approved" && !fixture.Inserted.CompletedTimestamp.HasValue)
                    {
                        throw new DataException("An approved assessment must include its completion timestamp.");
                    }

                    fixture.CaptureCommit();
                    return Task.CompletedTask;
                });
                DateTime? savedCompletion = null;
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 42 }))))
                    .Callback(() =>
                    {
                        Assert.Equal(2, fixture.CommittedSnapshots.Count);
                        var saved = fixture.CommittedSnapshots[1];
                        Assert.Equal("Approved", saved.Status);
                        Assert.Equal("Criteria met.", saved.Reason);
                        Assert.NotNull(saved.CompletedTimestamp);
                        savedCompletion = saved.CompletedTimestamp;
                        Assert.Equal(savedCompletion, fixture.Inserted.CompletedTimestamp);
                        fixture.Repository.Verify(repository => repository.CommitChangesAsync(), Times.Exactly(2));
                    })
                    .Returns(Task.CompletedTask);

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Approved", request.Status);
                Assert.Equal("Criteria met.", request.Reason);
                Assert.NotNull(savedCompletion);
                Assert.Equal(savedCompletion, request.CompletedTimestamp);
                Assert.Equal("Your namespace reservation request was approved and the namespace has been reserved for the requested owners. Criteria met.", result.Message);
                Assert.False(result.IsWarning);
                fixture.VerifyAllocation();
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Theory]
            [InlineData("Approved", "criteria_met")]
            [InlineData("Rejected", "criteria_not_met")]
            [InlineData("Rejected", "customer_information_required")]
            public async Task PersistsReasonAndCompletionOnTheSameRowAndDisplaysOnlyReason(string status, string reasonCode)
            {
                var fixture = new Fixture();
                var assessment = CreateAssessment(status, reasonCode, "Customer reason <b>is untrusted text</b>.");
                fixture.EnableAssessment(assessment);
                var before = DateTime.UtcNow;

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);
                var after = DateTime.UtcNow;

                Assert.Empty(result.Errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal(status, request.Status);
                Assert.Equal(assessment.Reason, request.Reason);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                var initial = fixture.CommittedSnapshots[0];
                var final = fixture.CommittedSnapshots[1];
                Assert.Equal("Pending", initial.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, initial.Reason);
                Assert.Null(initial.CompletedTimestamp);
                Assert.Equal(status, final.Status);
                Assert.Equal(assessment.Reason, final.Reason);
                Assert.Equal(initial.CreatedTimestamp, final.CreatedTimestamp);
                Assert.Equal(initial.Key, final.Key);
                Assert.Equal(initial.SubmittedByUserKey, final.SubmittedByUserKey);
                Assert.Equal(initial.Namespace, final.Namespace);
                Assert.Equal(initial.Justification, final.Justification);
                Assert.Equal(initial.RequestedOwnersJson, final.RequestedOwnersJson);
                Assert.Equal("We publish the Contoso tools.", final.Justification);
                if (status == "Pending")
                {
                    Assert.Null(request.CompletedTimestamp);
                }
                else
                {
                    Assert.True(request.CompletedTimestamp.HasValue);
                    Assert.Equal(DateTimeKind.Utc, request.CompletedTimestamp.Value.Kind);
                    Assert.InRange(request.CompletedTimestamp.Value, request.CreatedTimestamp, after);
                    Assert.InRange(request.CreatedTimestamp, before, after);
                }

                Assert.Equal(request.CompletedTimestamp, final.CompletedTimestamp);
                var expectedMessage = status == "Approved"
                    ? "Your namespace reservation request was approved and the namespace has been reserved for the requested owners. " + assessment.Reason
                    : "Your namespace reservation request was saved with status " + status + ". " + assessment.Reason + " No namespace has been reserved.";
                Assert.Equal(expectedMessage, result.Message);
                Assert.False(result.IsWarning);
                if (status == "Approved")
                {
                    fixture.VerifyAllocation();
                }
                else
                {
                    fixture.VerifyNoAllocation();
                }

                Assert.DoesNotContain(assessment.Rationale, result.Message);
                Assert.DoesNotContain("PRIVATE", result.Message);
                Assert.DoesNotContain("PRIVATE", request.Reason);
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
                fixture.EvidenceFactory.Verify(factory => factory.Create(42, It.Is<int[]>(keys => keys.SequenceEqual(new[] { 42 })), "Contoso.Tools"), Times.Once);
                fixture.Foundry.Verify(client => client.TestConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
                Assert.Equal(1, fixture.ResolveCount);
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task SavesPendingBeforeCreatingEvidenceAndSnapshotsTrustedScopeSeparatelyFromSubmittedFieldsBeforeAwait()
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "CanonicalOrganization", isAdmin: true);
                fixture.RegisterOwner("alias", organization);
                fixture.RegisterOwner("anotherAlias", organization);
                fixture.Input.Namespace = " Contoso.Tools ";
                fixture.Input.Owner = " alias, ALIAS, anotherAlias, alice ";
                fixture.Input.Justification = " We publish the Contoso tools. ";
                var assessment = CreateAssessment("Approved", "criteria_met", "Assessment criteria met.");
                fixture.EnableAssessment(assessment);
                var events = new List<string>();
                var initialCommit = new TaskCompletionSource<bool>();
                var commitCount = 0;
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(async () =>
                {
                    commitCount++;
                    if (commitCount == 1)
                    {
                        events.Add("initial commit started");
                        await initialCommit.Task;
                        fixture.Inserted.Key = 123;
                        fixture.CaptureCommit();
                        events.Add("initial Pending saved");
                    }
                    else
                    {
                        fixture.CaptureCommit();
                        events.Add("assessment saved");
                    }
                });
                fixture.OnResolve = () => events.Add("resolve client");
                fixture.EvidenceFactory.Setup(factory => factory.Create(42, It.Is<int[]>(keys => keys.SequenceEqual(new[] { 100, 42 })), "Contoso.Tools"))
                    .Callback(() =>
                    {
                        Assert.Single(fixture.CommittedSnapshots);
                        events.Add("create evidence");
                    })
                    .Returns(fixture.Evidence.Object);
                NamespaceReservationAssessmentInput modelInput = null;
                fixture.Foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None))
                    .Callback<NamespaceReservationAssessmentInput, INamespaceReservationEvidenceSession, CancellationToken>((input, evidence, token) =>
                    {
                        modelInput = input;
                        events.Add("assess");
                    })
                    .ReturnsAsync(assessment);
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    123, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 100, 42 }))))
                    .Callback(() =>
                    {
                        Assert.Equal(2, fixture.CommittedSnapshots.Count);
                        events.Add("reserve");
                    })
                    .Returns(Task.CompletedTask);

                fixture.VerifyNoResolution();
                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);
                Assert.False(submission.IsCompleted);
                fixture.VerifyNoResolution();
                Assert.Empty(fixture.CommittedSnapshots);
                Assert.Equal("Pending", fixture.Inserted.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, fixture.Inserted.Reason);
                Assert.Null(fixture.Inserted.CompletedTimestamp);
                AssertOwners(fixture.Inserted, organization, fixture.Submitter);

                // The caller and resolved entities can change while the first database await is outstanding.
                fixture.Input.Namespace = "Changed.Namespace";
                fixture.Input.Owner = "ChangedOwner";
                fixture.Input.Justification = "Changed justification.";
                organization.Username = "ChangedCanonicalName";
                organization.Key = 999;
                fixture.Submitter.Key = 888;
                fixture.Inserted.SubmittedByUserKey = 777;
                fixture.Inserted.RequestedOwnersJson = "[]";
                fixture.Inserted.Namespace = "Changed.Persisted.Namespace";
                initialCommit.SetResult(true);
                var result = await submission;

                Assert.Empty(result.Errors);
                Assert.Equal(new[] { "initial commit started", "initial Pending saved", "resolve client", "create evidence", "assess", "assessment saved", "reserve" }, events);
                Assert.NotNull(modelInput);
                Assert.Equal("{\"namespace\":\"Contoso.Tools\",\"owner\":\"alias, ALIAS, anotherAlias, alice\",\"justification\":\"We publish the Contoso tools.\"}", NamespaceReservationAssessmentContract.SerializeInput(modelInput));
                Assert.Equal("Pending", fixture.CommittedSnapshots[0].Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, fixture.CommittedSnapshots[0].Reason);
                Assert.Null(fixture.CommittedSnapshots[0].CompletedTimestamp);
                Assert.Equal(123, fixture.CommittedSnapshots[0].Key);
                Assert.Equal(123, fixture.CommittedSnapshots[1].Key);
                Assert.Equal("Approved", fixture.VerifySingleWrite().Status);
                fixture.Foundry.Verify(client => client.AssessAsync(modelInput, fixture.Evidence.Object, CancellationToken.None), Times.Once);
                fixture.EvidenceFactory.Verify(factory => factory.Create(42, It.Is<int[]>(keys => keys.SequenceEqual(new[] { 100, 42 })), "Contoso.Tools"), Times.Once);
                fixture.EvidenceFactory.Verify(factory => factory.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>()), Times.Once);
                fixture.VerifyAllocation(123, 100, 42);
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task AllocationUsesReadOnlyOriginalScopeAndInitialSavedKeyDespiteEvidenceAndRequestMutation()
            {
                var fixture = new Fixture();
                var organization = fixture.AddOrganization(100, "Contoso", isAdmin: true);
                fixture.Input.Owner = "Contoso, Alice, CONTOSO";
                fixture.Input.Namespace = " Contoso.Tools ";
                var assessment = CreateAssessment("Approved", "criteria_met", "Criteria met.");
                fixture.EnableAssessment(assessment);
                var modelCompletion = new TaskCompletionSource<NamespaceReservationAssessment>();
                int[] evidenceKeys = null;
                fixture.EvidenceFactory.Setup(factory => factory.Create(42, It.IsAny<int[]>(), "Contoso.Tools"))
                    .Callback<int, int[], string>((submitterKey, keys, namespaceValue) =>
                    {
                        Assert.Equal(7, Assert.Single(fixture.CommittedSnapshots).Key);
                        Assert.Equal(new[] { 100, 42 }, keys);
                        evidenceKeys = keys;
                        keys[0] = 999;
                        keys[1] = 888;
                        // The request key must already have been captured after the initial save.
                        fixture.Inserted.Key = 456;
                    })
                    .Returns(fixture.Evidence.Object);
                fixture.Foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None))
                    .Callback<NamespaceReservationAssessmentInput, INamespaceReservationEvidenceSession, CancellationToken>((input, evidence, token) =>
                    {
                        input.Namespace = "Changed.Model.Namespace";
                        input.Owner = "ChangedModelOwner";
                        input.Justification = "Changed model justification.";
                    })
                    .Returns(modelCompletion.Task);
                IReadOnlyCollection<int> reservationKeys = null;
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.IsAny<IReadOnlyCollection<int>>()))
                    .Callback<int, string, int, IReadOnlyCollection<int>>((requestKey, namespaceValue, submitterKey, keys) =>
                    {
                        Assert.Equal(new[] { 100, 42 }, keys);
                        Assert.NotSame(evidenceKeys, keys);
                        var list = Assert.IsAssignableFrom<IList<int>>(keys);
                        Assert.True(list.IsReadOnly);
                        Assert.Throws<NotSupportedException>(() => list[0] = 999);
                        Assert.Throws<NotSupportedException>(() => list.Add(999));
                        reservationKeys = keys;
                        var saved = fixture.CommittedSnapshots[1];
                        Assert.Equal("Approved", saved.Status);
                        Assert.NotNull(saved.CompletedTimestamp);
                        Assert.Equal(789, saved.Key);
                    })
                    .Returns(Task.CompletedTask);

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.False(submission.IsCompleted);
                fixture.VerifyNoAllocation();
                fixture.Input.Namespace = "Changed.Input.Namespace";
                fixture.Input.Owner = "ChangedOwner";
                fixture.Submitter.Key = 888;
                organization.Key = 999;
                fixture.Inserted.Key = 789;
                fixture.Inserted.Namespace = "Changed.Request.Namespace";
                fixture.Inserted.SubmittedByUserKey = 777;
                fixture.Inserted.RequestedOwnersJson = "[{\"Key\":666,\"Username\":\"ChangedOwner\"}]";
                modelCompletion.SetResult(assessment);
                var result = await submission;

                Assert.Empty(result.Errors);
                Assert.Equal("Your namespace reservation request was approved and the namespace has been reserved for the requested owners. Criteria met.", result.Message);
                Assert.Equal(new[] { 100, 42 }, reservationKeys);
                Assert.Equal(new[] { 999, 888 }, evidenceKeys);
                Assert.Equal(7, fixture.CommittedSnapshots[0].Key);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Approved", request.Status);
                Assert.Equal(assessment.Reason, request.Reason);
                Assert.Equal(fixture.CommittedSnapshots[1].CompletedTimestamp, request.CompletedTimestamp);
                fixture.VerifyAllocation(7, 100, 42);
                fixture.EvidenceFactory.Verify(factory => factory.Create(42, It.IsAny<int[]>(), "Contoso.Tools"), Times.Once);
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task InitialCommitAsyncFailureNeverResolvesEnabledClient()
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Do not assess."));
                var commit = new TaskCompletionSource<bool>();
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(commit.Task);

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);
                Assert.False(submission.IsCompleted);
                fixture.VerifyNoResolution();
                var expected = new DbUpdateException("PRIVATE initial database failure");
                commit.SetException(expected);

                Assert.Same(expected, await Assert.ThrowsAsync<DbUpdateException>(() => submission));
                fixture.VerifySingleWrite(expectedCommits: 1);
                fixture.VerifyNoResolution();
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task DisabledAssessmentPersistsSafeReasonAndReturnsItWithoutCallingModel()
            {
                var fixture = new Fixture();

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                Assert.Equal("Model assessment is disabled.", fixture.VerifySingleWrite().Reason);
                Assert.Equal("Your namespace reservation request was saved with status Pending. Model assessment is disabled. No namespace has been reserved.", result.Message);
                Assert.False(result.IsWarning);
                Assert.Null(fixture.Inserted.CompletedTimestamp);
                fixture.VerifyNoAssessment();
                Assert.Equal(1, fixture.ResolveCount);
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Theory]
            [InlineData("configuration")]
            [InlineData("authentication")]
            [InlineData("timeout")]
            [InlineData("malformed")]
            public async Task TechnicalFailuresPersistSafePendingReasonWithoutLeakingSecrets(string failure)
            {
                var fixture = new Fixture();
                const string secret = "PRIVATE token=secret; customer-response; SQL connection string";
                Exception exception;
                switch (failure)
                {
                    case "configuration": exception = new ConfigurationErrorsException(secret); break;
                    case "authentication": exception = new AuthenticationFailedException(secret); break;
                    case "timeout": exception = new OperationCanceledException(secret); break;
                    default: exception = new InvalidOperationException(secret, new Exception(secret)); break;
                }

                if (failure == "configuration")
                {
                    fixture.ResolutionException = exception;
                }
                else
                {
                    fixture.Foundry.SetupGet(client => client.IsEnabled).Returns(true);
                    fixture.Foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None)).ThrowsAsync(exception);
                }

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                var request = fixture.VerifySingleWrite();
                var reason = failure == "timeout"
                    ? "The model assessment timed out or was cancelled before a decision was accepted."
                    : "The model assessment could not be completed. No decision was accepted.";
                Assert.Equal("Pending", request.Status);
                Assert.Equal(reason, request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Equal(reason, fixture.CommittedSnapshots[1].Reason);
                Assert.Equal("Your namespace reservation request was saved with status Pending. " + reason + " No namespace has been reserved.", result.Message);
                Assert.False(result.IsWarning);
                Assert.DoesNotContain("PRIVATE", result.Message);
                Assert.DoesNotContain("PRIVATE", request.Reason);
                AssertSafeTelemetry(fixture, exception, "AssessNamespaceReservationRequest");
                Assert.Equal(1, fixture.ResolveCount);
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, It.IsAny<CancellationToken>()), failure == "configuration" ? Times.Never() : Times.Once());
                fixture.EvidenceFactory.Verify(factory => factory.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>()), failure == "configuration" ? Times.Never() : Times.Once());
                fixture.Foundry.Verify(client => client.TestConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            [Theory]
            [InlineData("exception")]
            [InlineData("timeout")]
            [InlineData("nullSession")]
            public async Task EvidenceFactoryFailurePersistsSanitizedPendingWithoutFallback(string failure)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Must not accept a decision without evidence."));
                Exception exception = failure == "timeout"
                    ? (Exception)new OperationCanceledException("PRIVATE SQL credentials")
                    : new InvalidOperationException("PRIVATE SQL credentials");
                var setup = fixture.EvidenceFactory.Setup(factory => factory.Create(42,
                    It.Is<int[]>(keys => keys.SequenceEqual(new[] { 42 })), "Contoso.Tools"))
                    .Callback(() =>
                    {
                        var initial = Assert.Single(fixture.CommittedSnapshots);
                        Assert.Equal("Pending", initial.Status);
                        Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, initial.Reason);
                    });
                if (failure == "nullSession")
                {
                    setup.Returns((INamespaceReservationEvidenceSession)null);
                }
                else
                {
                    setup.Throws(exception);
                }

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                var request = fixture.VerifySingleWrite();
                var reason = failure == "timeout"
                    ? "The model assessment timed out or was cancelled before a decision was accepted."
                    : "The model assessment could not be completed. No decision was accepted.";
                Assert.Equal("Pending", request.Status);
                Assert.Equal(reason, request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                Assert.Equal(reason, fixture.CommittedSnapshots[1].Reason);
                Assert.Contains(reason, result.Message);
                Assert.False(result.IsWarning);
                Assert.DoesNotContain("PRIVATE", result.Message);
                AssertSafeTelemetry(fixture, exception, "AssessNamespaceReservationRequest");
                fixture.EvidenceFactory.Verify(factory => factory.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>()), Times.Once);
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), It.IsAny<INamespaceReservationEvidenceSession>(), It.IsAny<CancellationToken>()), Times.Never);
                fixture.Foundry.Verify(client => client.TestConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            [Theory]
            [InlineData("nullAssessment")]
            [InlineData("nullReason")]
            [InlineData("emptyReason")]
            [InlineData("blankReason")]
            [InlineData("oversizedReason")]
            [InlineData("unknownStatus")]
            public async Task InvalidClientRecommendationCannotBecomePersistedApproval(string invalid)
            {
                var fixture = new Fixture();
                var reason = invalid == "nullReason" ? null
                    : invalid == "emptyReason" ? ""
                    : invalid == "blankReason" ? " \t\r\n"
                    : invalid == "oversizedReason" ? new string('x', 4001)
                    : "PRIVATE model reason";
                var assessment = invalid == "nullAssessment" ? null
                    : CreateAssessment(invalid == "unknownStatus" ? "approved" : "Approved", "criteria_met", reason);
                fixture.EnableAssessment(assessment);

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Pending", request.Status);
                Assert.Equal("The model assessment could not be completed. No decision was accepted.", request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Contains(request.Reason, result.Message);
                Assert.False(result.IsWarning);
                Assert.DoesNotContain("PRIVATE", result.Message);
                AssertSafeTelemetry(fixture, new InvalidOperationException(), "AssessNamespaceReservationRequest");
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
            }

            [Theory]
            [InlineData("Approved", "database")]
            [InlineData("Rejected", "database")]
            [InlineData("Approved", "readOnly")]
            [InlineData("Approved", "data")]
            public async Task FailedSecondCommitRestoresInitialTrackingStateWithoutRetryOrClaimingDecisionSaved(string status, string failure)
            {
                var fixture = new Fixture();
                var reasonCode = status == "Approved" ? "criteria_met" : status == "Rejected" ? "criteria_not_met" : "review_required";
                var assessment = CreateAssessment(status, reasonCode, "Model decision reason must not be shown after uncertain save.");
                fixture.EnableAssessment(assessment);
                Exception exception = failure == "readOnly" ? (Exception)new ReadOnlyModeException("PRIVATE database secret")
                    : failure == "data" ? new DataException("PRIVATE database secret")
                    : new DbUpdateException("PRIVATE database secret");
                var commitCount = 0;
                string attemptedStatus = null;
                string attemptedReason = null;
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(() =>
                {
                    if (++commitCount == 1)
                    {
                        fixture.CaptureCommit();
                        return Task.CompletedTask;
                    }

                    attemptedStatus = fixture.Inserted.Status;
                    attemptedReason = fixture.Inserted.Reason;
                    return Task.FromException(exception);
                });

                var result = await fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.Empty(result.Errors);
                Assert.Equal(status, attemptedStatus);
                Assert.Equal(assessment.Reason, attemptedReason);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Pending", request.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Single(fixture.CommittedSnapshots);
                Assert.Equal("Your namespace reservation request was saved, but we could not confirm that the assessment decision was saved. No namespace has been reserved.", result.Message);
                Assert.True(result.IsWarning);
                Assert.DoesNotContain(assessment.Reason, result.Message);
                Assert.DoesNotContain("saved with status", result.Message);
                Assert.DoesNotContain("PRIVATE", result.Message);
                AssertSafeTelemetry(fixture, exception, "SaveNamespaceReservationAssessment");
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
                Assert.Equal(1, fixture.ResolveCount);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task FailedOutstandingApprovalCommitNeverAllocatesEvenForUnexpectedExceptions(bool unexpected)
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Criteria met."));
                var finalCommit = new TaskCompletionSource<bool>();
                var commitCount = 0;
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(() =>
                {
                    if (++commitCount == 1)
                    {
                        fixture.CaptureCommit();
                        return Task.CompletedTask;
                    }

                    return finalCommit.Task;
                });
                Exception exception = unexpected ? (Exception)new InvalidOperationException("PRIVATE unexpected save failure")
                    : new DbUpdateException("PRIVATE database save failure");

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.False(submission.IsCompleted);
                Assert.Equal("Approved", fixture.Inserted.Status);
                Assert.NotNull(fixture.Inserted.CompletedTimestamp);
                Assert.Equal("Pending", Assert.Single(fixture.CommittedSnapshots).Status);
                fixture.VerifyNoAllocation();
                finalCommit.SetException(exception);
                if (unexpected)
                {
                    Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => submission));
                    Assert.Empty(fixture.TelemetryExceptions);
                }
                else
                {
                    var result = await submission;
                    Assert.Empty(result.Errors);
                    Assert.Equal("Your namespace reservation request was saved, but we could not confirm that the assessment decision was saved. No namespace has been reserved.", result.Message);
                    Assert.True(result.IsWarning);
                    Assert.Equal("Pending", fixture.Inserted.Status);
                    Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, fixture.Inserted.Reason);
                    Assert.Null(fixture.Inserted.CompletedTimestamp);
                    AssertSafeTelemetry(fixture, exception, "SaveNamespaceReservationAssessment");
                }

                var saved = Assert.Single(fixture.CommittedSnapshots);
                Assert.Equal("Pending", saved.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, saved.Reason);
                Assert.Null(saved.CompletedTimestamp);
                fixture.VerifyNoAllocation();
                fixture.VerifySingleWrite();
                fixture.Repository.VerifyNoOtherCalls();
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
            }

            [Fact]
            public async Task KeepsDurablePendingRowUntilModelCompletesBeforeCommittingDecision()
            {
                var fixture = new Fixture();
                var assessment = new TaskCompletionSource<NamespaceReservationAssessment>();
                fixture.Foundry.SetupGet(client => client.IsEnabled).Returns(true);
                fixture.Foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None))
                    .Returns(assessment.Task);

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.False(submission.IsCompleted);
                var request = fixture.VerifySingleWrite(expectedCommits: 1);
                Assert.Equal("Pending", request.Status);
                Assert.Equal(NamespaceReservationRequestService.InitialPendingReason, request.Reason);
                Assert.Null(request.CompletedTimestamp);
                Assert.Single(fixture.CommittedSnapshots);
                assessment.SetResult(CreateAssessment("Rejected", "criteria_not_met", "The submitted namespace does not meet the criteria."));
                var result = await submission;

                Assert.Empty(result.Errors);
                Assert.Same(request, fixture.VerifySingleWrite());
                Assert.Equal("Rejected", request.Status);
                Assert.Contains(request.Reason, result.Message);
                Assert.False(result.IsWarning);
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
            }

            [Fact]
            public async Task DoesNotReturnSuccessWhileSecondCommitIsOutstanding()
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Criteria met."));
                var finalCommit = new TaskCompletionSource<bool>();
                var commitCount = 0;
                fixture.Repository.Setup(repository => repository.CommitChangesAsync()).Returns(async () =>
                {
                    if (++commitCount == 2)
                    {
                        await finalCommit.Task;
                    }

                    fixture.CaptureCommit();
                });
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 42 }))))
                    .Callback(() =>
                    {
                        Assert.True(finalCommit.Task.IsCompleted);
                        Assert.Equal(2, fixture.CommittedSnapshots.Count);
                        Assert.Equal("Approved", fixture.CommittedSnapshots[1].Status);
                        Assert.NotNull(fixture.CommittedSnapshots[1].CompletedTimestamp);
                    })
                    .Returns(Task.CompletedTask);

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.False(submission.IsCompleted);
                Assert.Equal("Pending", Assert.Single(fixture.CommittedSnapshots).Status);
                fixture.VerifyNoAllocation();
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
                fixture.VerifySingleWrite();
                finalCommit.SetResult(true);
                var result = await submission;
                Assert.Empty(result.Errors);
                Assert.False(result.IsWarning);
                Assert.Equal("Your namespace reservation request was approved and the namespace has been reserved for the requested owners. Criteria met.", result.Message);
                fixture.VerifyAllocation();
                fixture.VerifySingleWrite();
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Fact]
            public async Task DoesNotReturnSuccessWhileAllocationIsOutstanding()
            {
                var fixture = new Fixture();
                fixture.EnableAssessment(CreateAssessment("Approved", "criteria_met", "Criteria met."));
                var allocation = new TaskCompletionSource<bool>();
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 42 }))))
                    .Returns(allocation.Task);

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);

                Assert.False(submission.IsCompleted);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                var saved = fixture.CommittedSnapshots[1];
                Assert.Equal("Approved", saved.Status);
                Assert.NotNull(saved.CompletedTimestamp);
                fixture.VerifySingleWrite();
                fixture.VerifyAllocation();
                allocation.SetResult(true);
                var result = await submission;

                Assert.Empty(result.Errors);
                Assert.False(result.IsWarning);
                Assert.Equal("Your namespace reservation request was approved and the namespace has been reserved for the requested owners. Criteria met.", result.Message);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Approved", request.Status);
                Assert.Equal(saved.Reason, request.Reason);
                Assert.Equal(saved.CompletedTimestamp, request.CompletedTimestamp);
                fixture.VerifyAllocation();
                Assert.Empty(fixture.TelemetryExceptions);
            }

            [Theory]
            [InlineData("precommit", false)]
            [InlineData("precommit", true)]
            [InlineData("postcommit", false)]
            [InlineData("audit", false)]
            public async Task AllocationFailurePreservesSavedApprovalWithoutRetryCompensationOrSecretExposure(string failure, bool synchronous)
            {
                var fixture = new Fixture();
                var assessment = CreateAssessment("Approved", "criteria_met", "Criteria met.");
                fixture.EnableAssessment(assessment);
                const string secret = "PRIVATE token=secret; SQL connection string; audit contents";
                Exception exception = failure == "precommit" ? (Exception)new DataException(secret)
                    : failure == "postcommit" ? new DbUpdateException(secret, new Exception(secret))
                    : new InvalidOperationException(secret, new Exception(secret));
                var allocation = new TaskCompletionSource<bool>();
                // Simulate the collaborator's durable side effects, not a live transaction:
                // post-commit/audit failures can leave both reservations already persisted.
                var durableReservations = new List<string>();
                fixture.ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 42 }))))
                    .Returns(() =>
                    {
                        Assert.Equal(2, fixture.CommittedSnapshots.Count);
                        Assert.Equal("Approved", fixture.CommittedSnapshots[1].Status);
                        Assert.NotNull(fixture.CommittedSnapshots[1].CompletedTimestamp);
                        if (failure != "precommit")
                        {
                            durableReservations.AddRange(new[] { "Contoso.Tools", "Contoso.Tools." });
                        }

                        if (synchronous)
                        {
                            throw exception;
                        }

                        return allocation.Task;
                    });

                var submission = fixture.Target.SubmitAsync(fixture.Submitter, fixture.Input);
                var saved = fixture.CommittedSnapshots[1];
                if (!synchronous)
                {
                    Assert.False(submission.IsCompleted);
                    fixture.VerifyAllocation();
                    fixture.VerifySingleWrite();
                    allocation.SetException(exception);
                }

                var result = await submission;

                Assert.Empty(result.Errors);
                Assert.Equal("Your request was approved, but automatic namespace reservation could not be confirmed. Check the table or contact support before retrying.", result.Message);
                Assert.True(result.IsWarning);
                Assert.DoesNotContain("PRIVATE", result.Message);
                Assert.DoesNotContain(assessment.Rationale, result.Message);
                Assert.DoesNotContain(assessment.Reason, result.Message);
                var request = fixture.VerifySingleWrite();
                Assert.Equal("Approved", request.Status);
                Assert.Equal(assessment.Reason, request.Reason);
                Assert.Equal(saved.Reason, request.Reason);
                Assert.NotNull(saved.CompletedTimestamp);
                Assert.Equal(saved.CompletedTimestamp, request.CompletedTimestamp);
                Assert.Equal(saved.Key, request.Key);
                Assert.Equal(saved.Namespace, request.Namespace);
                Assert.Equal(saved.SubmittedByUserKey, request.SubmittedByUserKey);
                Assert.Equal(saved.RequestedOwnersJson, request.RequestedOwnersJson);
                Assert.Equal(saved.CreatedTimestamp, request.CreatedTimestamp);
                Assert.Equal(saved.Justification, request.Justification);
                Assert.Equal(2, fixture.CommittedSnapshots.Count);
                Assert.Equal(failure == "precommit" ? Array.Empty<string>() : new[] { "Contoso.Tools", "Contoso.Tools." }, durableReservations);
                fixture.VerifyAllocation();
                fixture.Repository.VerifyNoOtherCalls();
                fixture.Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), fixture.Evidence.Object, CancellationToken.None), Times.Once);
                AssertSafeTelemetry(fixture, exception, "ReserveApprovedNamespaceRequest");
            }
        }

        private static NamespaceReservationAssessment CreateAssessment(string status, string reasonCode, string reason)
        {
            return new NamespaceReservationAssessment(status, reasonCode, reason,
                "PRIVATE internal rationale must not be displayed.",
                new[] { "NAME-RIGHTS" }, new[] { "justification" },
                reasonCode == "customer_information_required" ? new[] { "PRIVATE supporting details" } : Array.Empty<string>(),
                13, 7);
        }

        private static void AssertSafeTelemetry(Fixture fixture, Exception original, string operation)
        {
            var exception = Assert.IsType<InvalidOperationException>(Assert.Single(fixture.TelemetryExceptions));
            Assert.NotSame(original, exception);
            Assert.Null(exception.InnerException);
            Assert.Equal("Namespace reservation assessment processing failed.", exception.Message);
            Assert.DoesNotContain("PRIVATE", exception.ToString());
            var properties = Assert.Single(fixture.TelemetryProperties);
            Assert.Equal(2, properties.Count);
            Assert.Equal(operation, properties["Operation"]);
            Assert.Equal(original.GetType().Name, properties["ExceptionType"]);
            Assert.All(properties.Values, value => Assert.DoesNotContain("PRIVATE", value));
            fixture.Telemetry.Verify(service => service.TrackException(It.IsAny<Exception>(), It.IsAny<Action<Dictionary<string, string>>>()), Times.Once);
        }

        private static NamespaceReservationRequestInput CreateInput()
        {
            return new NamespaceReservationRequestInput
            {
                Namespace = "Contoso.Tools",
                Owner = "Alice",
                Justification = "We publish the Contoso tools."
            };
        }

        private static void SetField(NamespaceReservationRequestInput input, string field, string value)
        {
            switch (field)
            {
                case nameof(NamespaceReservationRequestInput.Namespace):
                    input.Namespace = value;
                    break;
                case nameof(NamespaceReservationRequestInput.Owner):
                    input.Owner = value;
                    break;
                case nameof(NamespaceReservationRequestInput.Justification):
                    input.Justification = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        private static void AssertFieldError(IReadOnlyCollection<ValidationResult> errors, string field)
        {
            var error = Assert.Single(errors);
            Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
            Assert.Equal(new[] { field }, error.MemberNames);
        }

        private static void AssertOwners(NamespaceReservationRequest request, params User[] expectedOwners)
        {
            var owners = JArray.Parse(request.RequestedOwnersJson);
            Assert.Equal(expectedOwners.Length, owners.Count);
            for (var i = 0; i < expectedOwners.Length; i++)
            {
                var owner = Assert.IsType<JObject>(owners[i]);
                Assert.Equal(new[] { "Key", "Username" }, owner.Properties().Select(property => property.Name));
                Assert.Equal(JTokenType.Integer, owner["Key"].Type);
                Assert.Equal(expectedOwners[i].Key, owner["Key"].Value<int>());
                Assert.Equal(JTokenType.String, owner["Username"].Type);
                Assert.Equal(expectedOwners[i].Username, owner["Username"].Value<string>());
            }
        }

        private class Fixture
        {
            private readonly List<NamespaceReservationRequest> _inserted = new List<NamespaceReservationRequest>();

            public Fixture()
            {
                Repository = new Mock<IEntityRepository<NamespaceReservationRequest>>(MockBehavior.Strict);
                Repository.Setup(repository => repository.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()))
                    .Callback<NamespaceReservationRequest>(request => _inserted.Add(request));
                Repository.Setup(repository => repository.CommitChangesAsync())
                    .Callback(CaptureCommit)
                    .Returns(Task.CompletedTask);
                UserService = new Mock<IUserService>(MockBehavior.Strict);
                UserService.Setup(service => service.FindByUsername(It.IsAny<string>(), false)).Returns((User)null);
                Submitter = new User("Alice")
                {
                    Key = 42,
                    EmailAddress = "alice@example.test"
                };
                RegisterOwner(Submitter.Username, Submitter);
                Input = CreateInput();
                Foundry = new Mock<INamespaceReservationFoundryClient>(MockBehavior.Strict);
                Foundry.SetupGet(client => client.IsEnabled).Returns(false);
                LazyFoundry = new Lazy<INamespaceReservationFoundryClient>(() =>
                {
                    ResolveCount++;
                    OnResolve?.Invoke();
                    if (ResolutionException != null)
                    {
                        throw ResolutionException;
                    }

                    return Foundry.Object;
                });
                Telemetry = new Mock<ITelemetryService>(MockBehavior.Strict);
                Telemetry.Setup(service => service.TrackException(It.IsAny<Exception>(), It.IsAny<Action<Dictionary<string, string>>>()))
                    .Callback<Exception, Action<Dictionary<string, string>>>((exception, addProperties) =>
                    {
                        var properties = new Dictionary<string, string>();
                        addProperties(properties);
                        TelemetryExceptions.Add(exception);
                        TelemetryProperties.Add(properties);
                    });
                Evidence = new Mock<INamespaceReservationEvidenceSession>(MockBehavior.Strict);
                EvidenceFactory = new Mock<INamespaceReservationEvidenceFactory>(MockBehavior.Strict);
                EvidenceFactory.Setup(factory => factory.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>()))
                    .Returns(Evidence.Object);
                ReservedNamespaceService = new Mock<IReservedNamespaceService>(MockBehavior.Strict);
                ReservedNamespaceService.Setup(service => service.ReserveNamespaceForRequestAsync(
                    7, "Contoso.Tools", 42, It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(new[] { 42 }))))
                    .Returns(Task.CompletedTask);
                Target = new NamespaceReservationRequestService(Repository.Object, UserService.Object, LazyFoundry, Telemetry.Object, EvidenceFactory.Object, ReservedNamespaceService.Object);
            }

            public Mock<IEntityRepository<NamespaceReservationRequest>> Repository { get; }
            public Mock<IReservedNamespaceService> ReservedNamespaceService { get; }
            public Mock<IUserService> UserService { get; }
            public Mock<INamespaceReservationFoundryClient> Foundry { get; }
            public Mock<INamespaceReservationEvidenceFactory> EvidenceFactory { get; }
            public Mock<INamespaceReservationEvidenceSession> Evidence { get; }
            public Lazy<INamespaceReservationFoundryClient> LazyFoundry { get; }
            public Mock<ITelemetryService> Telemetry { get; }
            public List<Exception> TelemetryExceptions { get; } = new List<Exception>();
            public List<Dictionary<string, string>> TelemetryProperties { get; } = new List<Dictionary<string, string>>();
            public List<NamespaceReservationRequest> CommittedSnapshots { get; } = new List<NamespaceReservationRequest>();
            public int ResolveCount { get; private set; }
            public Action OnResolve { get; set; }
            public Exception ResolutionException { get; set; }
            public NamespaceReservationRequest Inserted => Assert.Single(_inserted);
            public User Submitter { get; }
            public NamespaceReservationRequestInput Input { get; }
            public NamespaceReservationRequestService Target { get; }

            public async Task<IReadOnlyCollection<ValidationResult>> SubmitAsync()
            {
                return (await Target.SubmitAsync(Submitter, Input)).Errors;
            }

            public void CaptureCommit()
            {
                var request = Inserted;
                // Simulate identity assignment only on a successful initial save. Preserve keys
                // explicitly assigned by tests, including changes after that first save.
                if (CommittedSnapshots.Count == 0 && request.Key == 0)
                {
                    request.Key = 7;
                }

                CommittedSnapshots.Add(new NamespaceReservationRequest
                {
                    Key = request.Key,
                    SubmittedByUserKey = request.SubmittedByUserKey,
                    Namespace = request.Namespace,
                    RequestedOwnersJson = request.RequestedOwnersJson,
                    Justification = request.Justification,
                    CreatedTimestamp = request.CreatedTimestamp,
                    Status = request.Status,
                    Reason = request.Reason,
                    CompletedTimestamp = request.CompletedTimestamp
                });
            }

            public void EnableAssessment(NamespaceReservationAssessment assessment)
            {
                Foundry.SetupGet(client => client.IsEnabled).Returns(true);
                Foundry.Setup(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), Evidence.Object, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(assessment);
            }

            public void VerifyNoAssessment()
            {
                VerifyNoAllocation();
                Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), It.IsAny<INamespaceReservationEvidenceSession>(), It.IsAny<CancellationToken>()), Times.Never);
                Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), It.IsAny<CancellationToken>()), Times.Never);
                Foundry.Verify(client => client.TestConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
                VerifyNoEvidence();
            }

            public void VerifyNoEvidence()
            {
                EvidenceFactory.Verify(factory => factory.Create(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<string>()), Times.Never);
                Evidence.Verify(session => session.ExecuteAsync(It.IsAny<string>(), It.IsAny<JObject>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            public void VerifyNoResolution()
            {
                Assert.Equal(0, ResolveCount);
                Assert.False(LazyFoundry.IsValueCreated);
                Foundry.VerifyGet(client => client.IsEnabled, Times.Never);
                VerifyNoAssessment();
            }

            public void RegisterOwner(string lookupName, User owner)
            {
                UserService.Setup(service => service.FindByUsername(
                    It.Is<string>(name => string.Equals(name, lookupName, StringComparison.OrdinalIgnoreCase)), false))
                    .Returns(owner);
            }

            public Organization AddOrganization(int key, string username, bool isAdmin)
            {
                var organization = new Organization(username)
                {
                    Key = key,
                    EmailAddress = username + "@example.test"
                };
                organization.Members.Add(new Membership
                {
                    Organization = organization,
                    OrganizationKey = organization.Key,
                    Member = Submitter,
                    MemberKey = Submitter.Key,
                    IsAdmin = isAdmin
                });
                RegisterOwner(username, organization);
                return organization;
            }

            public NamespaceReservationRequest VerifySingleWrite(int expectedCommits = 2)
            {
                var request = Assert.Single(_inserted);
                if (request.Status != "Approved")
                {
                    VerifyNoAllocation();
                }

                Repository.Verify(repository => repository.InsertOnCommit(request), Times.Once);
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()), Times.Once);
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<IEnumerable<NamespaceReservationRequest>>()), Times.Never);
                Repository.Verify(repository => repository.CommitChangesAsync(), Times.Exactly(expectedCommits));
                // The service must never fall back to the less-verified overload or execute SQL tools itself.
                Foundry.Verify(client => client.AssessAsync(It.IsAny<NamespaceReservationAssessmentInput>(), It.IsAny<CancellationToken>()), Times.Never);
                Evidence.Verify(session => session.ExecuteAsync(It.IsAny<string>(), It.IsAny<JObject>(), It.IsAny<CancellationToken>()), Times.Never);
                return request;
            }

            public void VerifyAllocation(int requestKey = 7, params int[] ownerKeys)
            {
                var expectedOwnerKeys = ownerKeys.Length == 0 ? new[] { 42 } : ownerKeys;
                ReservedNamespaceService.Verify(service => service.ReserveNamespaceForRequestAsync(
                    requestKey, "Contoso.Tools", 42,
                    It.Is<IReadOnlyCollection<int>>(keys => keys.SequenceEqual(expectedOwnerKeys))), Times.Once);
                ReservedNamespaceService.VerifyNoOtherCalls();
            }

            public void VerifyNoAllocation()
            {
                ReservedNamespaceService.Verify(service => service.ReserveNamespaceForRequestAsync(
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()), Times.Never);
                ReservedNamespaceService.VerifyNoOtherCalls();
            }

            public void VerifyNoWrites()
            {
                Assert.Empty(_inserted);
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<NamespaceReservationRequest>()), Times.Never);
                Repository.Verify(repository => repository.InsertOnCommit(It.IsAny<IEnumerable<NamespaceReservationRequest>>()), Times.Never);
                Repository.Verify(repository => repository.CommitChangesAsync(), Times.Never);
                VerifyNoResolution();
            }

            public void VerifyNoOwnerLookups()
            {
                UserService.Verify(service => service.FindByUsername(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

        }
    }
}