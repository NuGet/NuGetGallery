// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class UserServiceFacts
    {
        public class TheAddMembershipRequestAsyncMethod
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenOrganizationIsNull_ThrowsException(bool isAdmin)
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.AddMembershipRequestAsync(null, "member", isAdmin);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            public static IEnumerable<object[]> WhenAlreadyAMember_ThrowsEntityException_Data
            {
                get
                {
                    foreach (var user in new Func<Fakes, User>[] {
                        (Fakes fakes) => fakes.OrganizationCollaborator,
                        (Fakes fakes) => fakes.OrganizationAdmin })
                    {
                        foreach (var isAdmin in new[] { false, true })
                        {
                            yield return MemberDataHelper.AsData(user, isAdmin);
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WhenAlreadyAMember_ThrowsEntityException_Data))]
            public async Task WhenAlreadyAMember_ThrowsEntityException(Func<Fakes, User> getUser, bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var user = getUser(fakes);
                var service = new TestableUserService();

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.AddMembershipRequestAsync(fakes.Organization, user.Username, isAdmin);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.AddMember_AlreadyAMember, user.Username), e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task WhenExistingRequest_ReturnsExistingRequest(bool existingRequestIsAdmin, bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                var existingRequest = new MembershipRequest
                {
                    NewMember = fakes.User,
                    ConfirmationToken = "token",
                    IsAdmin = existingRequestIsAdmin
                };

                fakes.Organization.MemberRequests.Add(existingRequest);

                // Act
                var request = await service.AddMembershipRequestAsync(fakes.Organization, fakes.User.Username, isAdmin);

                // Assert
                Assert.Equal(existingRequest, request);
                Assert.Equal(existingRequestIsAdmin || isAdmin, request.IsAdmin);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenMemberNotFound_ThrowEntityException(bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();
                service.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        fakes.Organization
                    }.AsQueryable());

                // Act
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.AddMembershipRequestAsync(fakes.Organization, fakes.User.Username, isAdmin);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.AddMember_UserNotFound, fakes.User.Username), e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenMemberNotConfirmed_ThrowEntityException(bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();
                service.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        fakes.Organization,
                        fakes.User
                    }.AsQueryable());

                fakes.User.EmailAddress = string.Empty;
                fakes.User.UnconfirmedEmailAddress = "unconfirmed@email.com";

                // Act
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.AddMembershipRequestAsync(fakes.Organization, fakes.User.Username, isAdmin);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.AddMember_UserNotConfirmed, fakes.User.Username), e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSecurityPolicyEvaluationFails_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                var error = "error";

                service.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, fakes.Organization, fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult(error)));

                service.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        fakes.Organization,
                        fakes.User
                    }.AsQueryable());

                // Act
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.AddMembershipRequestAsync(fakes.Organization, fakes.User.Username, isAdmin);
                });

                Assert.Equal(error, e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSecurityPolicyEvaluationSucceeds_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                service.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, fakes.Organization, fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));

                service.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        fakes.Organization,
                        fakes.User
                    }.AsQueryable());

                // Act
                var request = await service.AddMembershipRequestAsync(fakes.Organization, fakes.User.Username, isAdmin);
                Assert.Equal(isAdmin, request.IsAdmin);
                Assert.Equal(fakes.User, request.NewMember);
                Assert.Equal(fakes.Organization, request.Organization);
                Assert.Contains(request, fakes.Organization.MemberRequests);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheAddMemberAsyncMethod
        {
            public Fakes Fakes { get; }

            public TestableUserService UserService { get; }

            public TheAddMemberAsyncMethod()
            {
                Fakes = new Fakes();
                UserService = new TestableUserService();
            }

            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await UserService.AddMemberAsync(null, "member", "token");
                });

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenRequestNotFound_ThrowsEntityException()
            {
                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, "token");
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_MissingRequest, Fakes.User.Username),
                    e.Message);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenRequestWithTokenNotFound_ThrowsEntityException(bool isAdmin)
            {
                // Arrange
                UserService.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, Fakes.Organization, Fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("error")));

                Fakes.Organization.MemberRequests.Add(new MembershipRequest
                {
                    NewMember = Fakes.OrganizationCollaborator,
                    ConfirmationToken = "token",
                    IsAdmin = isAdmin
                });

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, "wrongToken");
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_MissingRequest, Fakes.User.Username),
                    e.Message);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenMemberExists_UpdatesMember(bool isAdmin)
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();
                service.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        fakes.User,
                        fakes.Organization
                    }.AsQueryable());

                var token = "token";

                fakes.Organization.MemberRequests.Add(new MembershipRequest
                {
                    NewMember = fakes.OrganizationCollaborator,
                    ConfirmationToken = token,
                    IsAdmin = isAdmin
                });

                // Act
                var result = await service.AddMemberAsync(fakes.Organization, fakes.OrganizationCollaborator.Username, token);
                Assert.Equal(isAdmin, result.IsAdmin);
                Assert.Equal(fakes.OrganizationCollaborator, result.Member);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenRequestFoundWithUnconfirmedUser_ThrowsEntityException(bool isAdmin)
            {
                // Arrange
                var token = "token";

                Fakes.Organization.MemberRequests.Add(new MembershipRequest
                {
                    NewMember = Fakes.User,
                    ConfirmationToken = token
                });

                Fakes.User.EmailAddress = string.Empty;
                Fakes.User.UnconfirmedEmailAddress = "unconfirmed@email.com";

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, token);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_UserNotConfirmed, Fakes.User.Username),
                    e.Message);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenSecurityPolicyEvalutionFailure_ThrowsEntityException(bool isAdmin)
            {
                // Arrange
                var error = "error";

                UserService.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, Fakes.Organization, Fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult(error)));

                var token = "token";

                Fakes.Organization.MemberRequests.Add(new MembershipRequest
                {
                    NewMember = Fakes.User,
                    ConfirmationToken = token,
                    IsAdmin = isAdmin
                });

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, token);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                        Strings.AddMember_PolicyFailure, error), 
                        e.Message);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSecurityPolicyEvalutionSucceeds_CreatesMembership(bool isAdmin)
            {
                // Arrange
                UserService.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, Fakes.Organization, Fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));

                var token = "token";

                Fakes.Organization.MemberRequests.Add(new MembershipRequest
                {
                    NewMember = Fakes.User,
                    ConfirmationToken = token,
                    IsAdmin = isAdmin
                });

                // Act
                var result = await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, token);

                // Assert
                Assert.Equal(isAdmin, result.IsAdmin);
                Assert.Equal(Fakes.User, result.Member);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheDeleteMemberAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.DeleteMemberAsync(null, "member");
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenNoMatch_ThrowsEntityException()
            {
                // Arrange
                var service = new TestableUserService();

                var memberName = "member";

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.DeleteMemberAsync(new Organization(), memberName);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.UpdateOrDeleteMember_MemberNotFound, memberName),
                    e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenLastAdmin_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);

                Assert.Equal(
                    Strings.DeleteMember_CannotRemoveLastAdmin,
                    e.Message);
            }

            [Fact]
            public async Task WhenNotLastAdmin_DeletesMembership()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                foreach (var m in fakes.Organization.Members)
                {
                    m.IsAdmin = true;
                }

                // Act
                await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username);

                // Assert
                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task WhenCollaborator_DeletesMembership()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act
                await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationCollaborator.Username);

                // Assert
                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheRejectMembershipRequestAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.RejectMembershipRequestAsync(null, "member", "token");
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenNoExistingRequest_ThrowsEntityException()
            {
                // Arrange
                var service = new TestableUserService();

                var memberName = "member";

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.RejectMembershipRequestAsync(new Organization { MemberRequests = new MembershipRequest[0] }, memberName, "token");
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.RejectMembershipRequest_NotFound, memberName),
                    e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenExistingRequestWithDifferentToken_ThrowsEntityException()
            {
                // Arrange
                var service = new TestableUserService();

                var memberName = "member";
                var token = "token";

                var organization = new Organization
                {
                    MemberRequests = new List<MembershipRequest>
                    {
                        new MembershipRequest
                        {
                            NewMember = new User(memberName),
                            ConfirmationToken = "differentToken"
                        }
                    }
                };

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.RejectMembershipRequestAsync(organization, memberName, token);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.RejectMembershipRequest_NotFound, memberName),
                    e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WithExistingRequest_Succeeds()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                var memberName = "member";
                var token = "token";

                var organization = new Organization
                {
                    MemberRequests = new List<MembershipRequest>
                    {
                        new MembershipRequest
                        {
                            NewMember = new User(memberName),
                            ConfirmationToken = token
                        }
                    }
                };

                // Act & Assert
                await service.RejectMembershipRequestAsync(organization, memberName, token);

                Assert.True(!organization.MemberRequests.Any());

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheCancelMembershipRequestAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.CancelMembershipRequestAsync(null, "member");
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenNoExistingRequest_ThrowsEntityException()
            {
                // Arrange
                var service = new TestableUserService();

                var memberName = "member";

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.CancelMembershipRequestAsync(new Organization { MemberRequests = new MembershipRequest[0] }, memberName);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.CancelMembershipRequest_MissingRequest, memberName),
                    e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WithExistingRequest_Succeeds()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                var memberName = "member";

                var newMember = new User(memberName);

                var organization = new Organization
                {
                    MemberRequests = new List<MembershipRequest>
                    {
                        new MembershipRequest
                        {
                            NewMember = newMember,
                            ConfirmationToken = "token"
                        }
                    }
                };

                // Act & Assert
                var pendingMember = await service.CancelMembershipRequestAsync(organization, memberName);

                Assert.True(!organization.MemberRequests.Any());
                Assert.Equal(newMember, pendingMember);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheUpdateMemberAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.UpdateMemberAsync(null, "member", false);
                });
            }

            [Fact]
            public async Task WhenMemberNotFound_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.UpdateMemberAsync(fakes.Organization, fakes.User.Username, false);
                });

                Assert.Equal(string.Format(CultureInfo.CurrentCulture,
                    Strings.UpdateOrDeleteMember_MemberNotFound, fakes.User.Username),
                    e.Message);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenRemovingLastAdmin_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                var e = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.UpdateMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username, false);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
                Assert.Equal(
                    Strings.UpdateMember_CannotRemoveLastAdmin,
                    e.Message);
            }

            [Fact]
            public async Task WhenNotRemovingLastAdmin_ReturnsSuccess()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();
                foreach (var m in fakes.Organization.Members)
                {
                    m.IsAdmin = true;
                }

                // Act
                var result = await service.UpdateMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username, false);

                // Assert
                Assert.Equal(false, result.IsAdmin);
                Assert.Equal(fakes.OrganizationAdmin, result.Member);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheConfirmEmailAddressMethod
        {
            [Fact]
            public async Task WithTokenThatDoesNotMatchUserReturnsFalse()
            {
                var user = new User { Username = "username", EmailConfirmationToken = "token" };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "not-token");

                Assert.False(confirmed);
            }

            [Fact]
            public async Task ThrowsForDuplicateConfirmedEmailAddresses()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "old@example.org", UnconfirmedEmailAddress = "new@example.org", EmailConfirmationToken = "token" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var ex = await AssertEx.Throws<EntityException>(() => service.ConfirmEmailAddress(user, "token"));
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, "new@example.org"), ex.Message);
            }

            [Fact]
            public async Task WithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public async Task ForUserWithConfirmedEmailWithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    EmailAddress = "existing@example.com",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public async Task WithNullUserThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                await AssertEx.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(null, "token"));
            }

            [Fact]
            public async Task WithEmptyTokenThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                await AssertEx.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(new User(), ""));
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    EmailAddress = "existing@example.com",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ConfirmEmail &&
                    ar.AffectedEmailAddress == "new@example.com"));
            }
        }

        public class TheFindByEmailAddressMethod
        {
            [Fact]
            public void ReturnsNullIfMultipleMatchesExist()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "new@example.org" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var result = service.FindByEmailAddress("new@example.org");
                Assert.Null(result);
            }
        }

        public class TheChangeEmailMethod
        {
            [Fact]
            public async Task SetsUnconfirmedEmailWhenEmailIsChanged()
            {
                var user = new User { Username = "Bob", EmailAddress = "old@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.ChangeEmailAddress(user, "new@example.org");

                Assert.Equal("old@example.org", user.EmailAddress);
                Assert.Equal("new@example.org", user.UnconfirmedEmailAddress);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It has to change the pending confirmation token whenever address changes because otherwise you can do
            /// 1. change address, get confirmation email
            /// 2. change email address again to something you don't own
            /// 3. hit confirm and you confirmed an email address you don't own
            /// </summary>
            [Fact]
            public async Task ModifiesConfirmationTokenWhenEmailAddressChanged()
            {
                var user = new User { EmailAddress = "old@example.com", EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "new@example.com");
                Assert.NotNull(user.EmailConfirmationToken);
                Assert.NotEmpty(user.EmailConfirmationToken);
                Assert.NotEqual("pending-token", user.EmailConfirmationToken);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It would be annoying if you start seeing pending email changes as a result of NOT changing your email address.
            /// </summary>
            [Fact]
            public async Task DoesNotModifyAnythingWhenConfirmedEmailAddressNotChanged()
            {
                var user = new User { EmailAddress = "old@example.com", UnconfirmedEmailAddress = null, EmailConfirmationToken = null };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "old@example.com");
                Assert.True(user.Confirmed);
                Assert.Equal("old@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            /// <summary>
            /// Because it's bad if your confirmation email no longer works because you did a no-op email address change.
            /// </summary>
            [Theory]
            [InlineData("something@else.com")]
            [InlineData(null)]
            public async Task DoesNotModifyConfirmationTokenWhenUnconfirmedEmailAddressNotChanged(string confirmedEmailAddress)
            {
                var user = new User {
                    EmailAddress = confirmedEmailAddress,
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "old@example.com");
                Assert.Equal("pending-token", user.EmailConfirmationToken);
            }

            [Fact]
            public async Task DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User { EmailAddress = "old@example.com", Key = 1 };
                var conflictingUser = new User { EmailAddress = "new@example.com", Key = 2 };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user, conflictingUser },
                };

                var e = await AssertEx.Throws<EntityException>(() => service.ChangeEmailAddress(user, "new@example.com"));
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, "new@example.com"), e.Message);
                Assert.Equal("old@example.com", user.EmailAddress);
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var user = new User { Username = "Bob", EmailAddress = "old@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                // Act
                await service.ChangeEmailAddress(user, "new@example.org");

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ChangeEmail &&
                    ar.AffectedEmailAddress == "new@example.org" &&
                    ar.EmailAddress == "old@example.org"));
            }
        }

        public class TheCancelChangeEmailAddressMethod
        {
            [Fact]
            public async Task ClearsUnconfirmedEmail()
            {
                var user = new User { Username = "Bob", UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.CancelChangeEmailAddress(user);

                Assert.Equal("confirmedEmail@example.org", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            [Fact]
            public async Task ClearsEmailConfirmationToken()
            {
                var user = new User { Username = "Bob", EmailConfirmationToken = Guid.NewGuid().ToString() ,UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.CancelChangeEmailAddress(user);

                Assert.Equal("confirmedEmail@example.org", user.EmailAddress);
                Assert.Null(user.EmailConfirmationToken);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var user = new User { Username = "Bob", EmailConfirmationToken = Guid.NewGuid().ToString(), UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                // Act
                await service.CancelChangeEmailAddress(user);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.CancelChangeEmail &&
                    ar.AffectedEmailAddress == "unconfirmedEmail@example.org" &&
                    ar.EmailAddress == "confirmedEmail@example.org"));
            }
        }


        public class TheUpdateProfileMethod
        {
            [Fact]
            public async Task SavesEmailSettings()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true, NotifyPackagePushed = true};
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());
                
                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);
                
                // Enable contact notifications
                await service.ChangeEmailSubscriptionAsync(user, true, false);
                Assert.Equal(true, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Enable package pushed notifications
                await service.ChangeEmailSubscriptionAsync(user, false, true);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(true, user.NotifyPackagePushed);

                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Enable all notifications
                await service.ChangeEmailSubscriptionAsync(user, true, true);
                Assert.Equal(true, user.EmailAllowed);
                Assert.Equal(true, user.NotifyPackagePushed);

                service.MockUserRepository
                       .Verify(r => r.CommitChangesAsync());
            }

            [Fact]
            public async Task ThrowsArgumentExceptionForNullUser()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(async () => await service.ChangeEmailSubscriptionAsync(null, emailAllowed: true, notifyPackagePushed: true), "user");
            }
        }

        public class TheCanTransformToOrganizationMethod
        {
            [Fact]
            public void WhenAccountIsNotConfirmed_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var unconfirmedUser = new User() { UnconfirmedEmailAddress = "unconfirmed@example.com" };

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(unconfirmedUser, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountNotConfirmed, unconfirmedUser.Username));
            }

            [Fact]
            public void WhenAccountIsOrganization_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(fakes.Organization, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountIsAnOrganization, fakes.Organization.Username));
            }

            [Fact]
            public void WhenAccountHasMemberships_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(fakes.OrganizationCollaborator, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountHasMemberships, fakes.OrganizationCollaborator.Username));
            }

            [Fact]
            public void WhenAccountIsInWhitelist_ReturnsTrue()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, out errorReason);

                // Assert
                Assert.True(result);
            }
        }

        public class TheCanTransformToOrganizationWithAdminMethod
        {
            [Fact]
            public void WhenAdminMatchesAccountToTransform_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, user, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminMustBeDifferentAccount, user.Username));
            }

            [Fact]
            public void WhenAdminIsNotConfirmed_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var unconfirmedUser = new User() { UnconfirmedEmailAddress = "unconfirmed@example.com" };
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, unconfirmedUser, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountNotConfirmed, unconfirmedUser.Username));
            }

            [Fact]
            public void WhenAdminIsOrganization_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;
                var organization = fakes.Organization;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, organization, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountIsOrganization, organization.Username));
            }
        }

        public class TheRequestTransformToOrganizationAccountMethod
        {
            [Fact]
            public async Task WhenAccountIsNull_ThrowsNullRefException()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(
                    async () => await service.RequestTransformToOrganizationAccount(accountToTransform: null, adminUser: new User("admin")),
                    "accountToTransform");
            }

            [Fact]
            public async Task WhenAdminUserIsNull_ThrowsNullRefException()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(
                    async () => await service.RequestTransformToOrganizationAccount(accountToTransform: new User("account"), adminUser: null),
                    "adminUser");
            }

            [Fact]
            public Task WhenExistingRequest_Overwrites()
            {
                return VerifyCreatesRequest(testOverwrite: true);
            }

            [Fact]
            public Task WhenNoExistingRequest_CreatesNew()
            {
                return VerifyCreatesRequest(testOverwrite: false);
            }

            private async Task VerifyCreatesRequest(bool testOverwrite)
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                var admin = CreateAdminUser();

                service.MockUserRepository.Setup(r => r.CommitChangesAsync()).Returns(Task.CompletedTask).Verifiable();
                
                DateTime? requestDate = null;
                string requestToken = null;
                for (int i = 0; i < (testOverwrite ? 2 : 1); i++)
                {
                    // Act
                    await service.RequestTransformToOrganizationAccount(account, admin);

                    if (testOverwrite)
                    {
                        if (requestDate != null)
                        {
                            Assert.True(requestDate < account.OrganizationMigrationRequest.RequestDate);
                            Assert.NotEqual(requestToken, account.OrganizationMigrationRequest.ConfirmationToken);
                        }

                        requestDate = account.OrganizationMigrationRequest.RequestDate;
                        requestToken = account.OrganizationMigrationRequest.ConfirmationToken;
                        await Task.Delay(500); // ensure next requestDate is in future
                    }

                    // Assert
                    service.MockUserRepository.Verify(r => r.CommitChangesAsync(), Times.Once);
                    service.MockUserRepository.ResetCalls();

                    Assert.NotNull(account.OrganizationMigrationRequest);
                    Assert.Equal(account, account.OrganizationMigrationRequest.NewOrganization);
                    Assert.Equal(admin, account.OrganizationMigrationRequest.AdminUser);
                    Assert.False(String.IsNullOrEmpty(account.OrganizationMigrationRequest.ConfirmationToken));

                    if (testOverwrite)
                    {
                        admin = CreateAdminUser();
                    }
                }
            }

            private User CreateAdminUser()
            {
                var admin = new User($"Admin-{DateTime.UtcNow.Ticks}");
                admin.Credentials.Add(
                    new CredentialBuilder().CreateExternalCredential(
                        issuer: "MicrosoftAccount",
                        value: "abc123",
                        identity: "Admin",
                        tenantId: "zyx987"));
                return admin;
            }
        }

        public class TheTransformToOrganizationAccountMethod
        {
            private TestableUserService _service = new TestableUserService();

            [Fact]
            public async Task WhenAdminHasNoTenant_TransformsAccountWithoutPolicy()
            {
                Assert.True(await InvokeTransformUserToOrganization(3, new User("adminWithNoTenant") { Credentials = new Credential[0] }));
            }

            public async Task WhenAdminHasUnsupportedTenant_TransformsAccountWithoutPolicy()
            {
                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(false);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                Assert.True(await InvokeTransformUserToOrganization(3));
            }

            public async Task WhenAdminHasSupportedTenant_TransformsAccountWithPolicy()
            {
                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(true);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                Assert.True(await InvokeTransformUserToOrganization(3, subscribesToPolicy: true));
            }

            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            public async Task WhenSqlResultIsZeroOrLess_ReturnsFalse(int affectedRecords)
            {
                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(false);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                Assert.False(await InvokeTransformUserToOrganization(affectedRecords));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(3)]
            public async Task WhenSqlResultIsPositive_ReturnsTrue(int affectedRecords)
            {
                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(false);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                Assert.True(await InvokeTransformUserToOrganization(affectedRecords));
            }

            private Task<bool> InvokeTransformUserToOrganization(int affectedRecords, User admin = null, bool subscribesToPolicy = false)
            {
                // Arrange
                var account = new User("Account");
                admin = admin ?? new User("Admin")
                {
                    Credentials = new Credential[] {
                        new CredentialBuilder().CreateExternalCredential(
                            issuer: "AzureActiveDirectory",
                            value: "abc123",
                            identity: "Admin",
                            tenantId: "zyx987")
                    }
                };

                _service.MockDatabase
                    .Setup(db => db.ExecuteSqlResourceAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .Returns(Task.FromResult(affectedRecords));

                // Act
                var result = _service.TransformUserToOrganization(account, admin, "token");

                _service.MockSecurityPolicyService
                    .Verify(
                        sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), true), 
                        subscribesToPolicy ? Times.Once() : Times.Never());

                return result;
            }
        }
        
        public class TheAddOrganizationAccountMethod
        {
            private const string OrgName = "myOrg";
            private const string OrgEmail = "myOrg@myOrg.com";
            private const string AdminName = "orgAdmin";

            private static DateTime OrgCreatedUtc = new DateTime(2018, 2, 21);

            private TestableUserService _service = new TestableUserService();

            [Fact]
            public async Task WithUsernameConflict_ThrowsEntityException()
            {
                var conflictUsername = "ialreadyexist";

                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(new[] { new User(conflictUsername) }.MockDbSet().Object);

                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization(orgName: conflictUsername));
                Assert.Equal(String.Format(CultureInfo.CurrentCulture, Strings.UsernameNotAvailable, conflictUsername), exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Never());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WithEmailConflict_ThrowsEntityException()
            {
                var conflictEmail = "ialreadyexist@existence.com";

                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(new[] { new User("user") { EmailAddress = conflictEmail } }.MockDbSet().Object);

                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization(orgEmail: conflictEmail));
                Assert.Equal(String.Format(CultureInfo.CurrentCulture, Strings.EmailAddressBeingUsed, conflictEmail), exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Never());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WhenAdminHasNoTenant_ReturnsNewOrgWithoutPolicy()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                var org = await InvokeAddOrganization(admin: new User(AdminName) { Credentials = new Credential[0] });

                AssertNewOrganizationReturned(org, subscribedToPolicy: false);
            }

            [Fact]
            public async Task WhenAdminHasUnsupportedTenant_ReturnsNewOrgWithoutPolicy()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(false);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                var org = await InvokeAddOrganization();

                AssertNewOrganizationReturned(org, subscribedToPolicy: false);
            }

            [Fact]
            public async Task WhenSubscribingToPolicyFails_ReturnsNewOrgWithoutPolicy()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(true);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                _service.MockSecurityPolicyService
                    .Setup(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false))
                    .Returns(Task.FromResult(false));

                var org = await InvokeAddOrganization();

                AssertNewOrganizationReturned(org, subscribedToPolicy: true);
            }

            [Fact]
            public async Task WhenSubscribingToPolicySucceeds_ReturnsNewOrg()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                var mockLoginDiscontinuationConfiguration = new Mock<ILoginDiscontinuationConfiguration>();
                mockLoginDiscontinuationConfiguration
                    .Setup(x => x.IsTenantIdPolicySupportedForOrganization(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(true);

                _service.MockConfigObjectService
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(mockLoginDiscontinuationConfiguration.Object);

                _service.MockSecurityPolicyService
                    .Setup(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false))
                    .Returns(Task.FromResult(true));

                var org = await InvokeAddOrganization();

                AssertNewOrganizationReturned(org, subscribedToPolicy: true);
            }

            private Task<Organization> InvokeAddOrganization(string orgName = OrgName, string orgEmail = OrgEmail, User admin = null)
            {
                // Arrange
                admin = admin ?? new User(AdminName)
                {
                    Credentials = new Credential[] {
                        new CredentialBuilder().CreateExternalCredential(
                            issuer: "AzureActiveDirectory",
                            value: "abc123",
                            identity: "Admin",
                            tenantId: "zyx987")
                    }
                };

                _service.MockDateTimeProvider
                    .Setup(x => x.UtcNow)
                    .Returns(OrgCreatedUtc);

                // Act
                return _service.AddOrganizationAsync(orgName, orgEmail, admin);
            }

            private void AssertNewOrganizationReturned(Organization org, bool subscribedToPolicy)
            {
                Assert.Equal(OrgName, org.Username);
                Assert.Equal(OrgEmail, org.UnconfirmedEmailAddress);
                Assert.Equal(OrgCreatedUtc, org.CreatedUtc);
                Assert.True(org.EmailAllowed);
                Assert.True(org.NotifyPackagePushed);
                Assert.True(!string.IsNullOrEmpty(org.EmailConfirmationToken));

                // Both the organization and the admin must have a membership to each other.
                Func<Membership, bool> hasMembership = m => m.Member.Username == AdminName && m.Organization.Username == OrgName && m.IsAdmin;
                Assert.True(
                    org.Members.Any(
                        m => hasMembership(m) && m.Member.Organizations.Any(hasMembership)));

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Once());
                _service.MockSecurityPolicyService.Verify(
                    sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), 
                    subscribedToPolicy ? Times.Once() : Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once());
            }
        }
        public class TheRejectTransformUserToOrganizationRequestMethod
        {
            public async Task IfNoExistingRequest_ReturnsFalse()
            {
                var accountToTransform = new User("norequest");

                var service = new TestableUserService();

                var result = await service.RejectTransformUserToOrganizationRequest(accountToTransform, null, null);

                Assert.False(result);

                service.MockUserRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            public async Task IfAdminUserNull_ReturnsFalse()
            {
                var accountToTransform = new User("hasrequest") { OrganizationMigrationRequest = new OrganizationMigrationRequest() };

                var service = new TestableUserService();

                var result = await service.RejectTransformUserToOrganizationRequest(accountToTransform, null, null);

                Assert.False(result);

                service.MockUserRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            public async Task IfAdminUserDoesntMatchRequest_ReturnsFalse()
            {
                var admin = new User("requestAdmin");
                var wrongAdmin = new User("admin");
                var accountToTransform = new User("hasrequest") { OrganizationMigrationRequest = new OrganizationMigrationRequest { AdminUser = admin } };

                var service = new TestableUserService();

                var result = await service.RejectTransformUserToOrganizationRequest(accountToTransform, wrongAdmin, null);

                Assert.False(result);

                service.MockUserRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            public async Task IfTokenDoesntMatch_ReturnsFalse()
            {
                var admin = new User("admin");
                var accountToTransform = new User("hasrequest") { OrganizationMigrationRequest = new OrganizationMigrationRequest { AdminUser = admin, ConfirmationToken = "token" } };

                var service = new TestableUserService();

                var result = await service.RejectTransformUserToOrganizationRequest(accountToTransform, admin, "wrongToken");

                Assert.False(result);

                service.MockUserRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            public async Task IfTokenMatches_RemovesRequest()
            {
                var token = "token";
                var admin = new User("admin");
                var accountToTransform = new User("hasrequest") { OrganizationMigrationRequest = new OrganizationMigrationRequest { AdminUser = admin, ConfirmationToken = token } };

                var service = new TestableUserService();

                var result = await service.RejectTransformUserToOrganizationRequest(accountToTransform, admin, token);

                Assert.True(result);
                Assert.Null(accountToTransform.OrganizationMigrationRequest);

                service.MockUserRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
            }
        }
    }
}

