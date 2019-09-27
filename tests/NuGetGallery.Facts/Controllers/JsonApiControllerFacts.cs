// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Mail.Messages;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class JsonApiControllerFacts
    {
        public class ThePackageOwnerMethods : TestContainer
        {
            public class ThePackageOwnerModificationMethods : TestContainer
            {
                public static IEnumerable<object[]> ThrowsArgumentNullIfMissing_Data
                {
                    get
                    {
                        foreach (var request in _requests)
                        {
                            foreach (var missingId in _missingData)
                            {
                                yield return new object[]
                                {
                                    request,
                                    missingId,
                                };
                            }
                        }
                    }
                }

                [Theory]
                [MemberData(nameof(ThrowsArgumentNullIfMissing_Data))]
                public void ThrowsArgumentNullIfPackageIdMissing(InvokePackageOwnerModificationRequest request, string id)
                {
                    // Arrange
                    var controller = GetController<JsonApiController>();

                    // Act & Assert
                    Assert.ThrowsAsync<ArgumentException>(() => request(controller, id, "username"));
                }

                [Theory]
                [MemberData(nameof(ThrowsArgumentNullIfMissing_Data))]
                public void ThrowsArgumentNullIfUsernameMissing(InvokePackageOwnerModificationRequest request, string username)
                {
                    // Arrange
                    var controller = GetController<JsonApiController>();

                    // Act & Assert
                    Assert.ThrowsAsync<ArgumentException>(() => request(controller, "package", username));
                }

                [Theory]
                [MemberData(nameof(AllRequests_Data))]
                public async Task ReturnsFailureIfPackageNotFound(InvokePackageOwnerModificationRequest request)
                {
                    // Arrange
                    var controller = GetController<JsonApiController>();

                    // Act
                    var result = await request(controller, "package", "user");
                    dynamic data = ((JsonResult)result).Data;

                    // Assert
                    Assert.False(data.success);
                    Assert.Equal("Package not found.", data.message);
                }

                [Theory]
                [MemberData(nameof(AllCannotManagePackageOwnersByRequests_Data))]
                public async Task ReturnsFailureIfUserIsNotPackageOwner(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser)
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    var currentUser = getCurrentUser(fakes);
                    var controller = GetController<JsonApiController>();
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = await request(controller, fakes.Package.Id, "nonUser");
                    dynamic data = ((JsonResult)result).Data;

                    // Assert
                    Assert.False(data.success);

                    var message = currentUser == null ? "Current user not found." : "You are not the package owner.";
                    Assert.Equal(message, data.message);
                }

                [Theory]
                [MemberData(nameof(AllCanManagePackageOwnersByRequests_Data))]
                public async Task ReturnsFailureIfNewOwnerIsNotRealUser(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser)
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    var currentUser = getCurrentUser(fakes);
                    var controller = GetController<JsonApiController>();
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = await request(controller, fakes.Package.Id, "nonUser");
                    dynamic data = ((JsonResult)result).Data;

                    // Assert
                    Assert.False(data.success);
                    Assert.Equal("Owner not found.", data.message);
                }

                [Theory]
                [MemberData(nameof(AllCanManagePackageOwnersByRequests_Data))]
                public async Task ReturnsFailureIfNewOwnerIsNotConfirmed(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser)
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    var currentUser = getCurrentUser(fakes);
                    var controller = GetController<JsonApiController>();
                    controller.SetCurrentUser(currentUser);
                    fakes.User.UnconfirmedEmailAddress = fakes.User.EmailAddress;
                    fakes.User.EmailAddress = null;

                    // Act
                    var result = await request(controller, fakes.Package.Id, fakes.User.Username);
                    dynamic data = ((JsonResult)result).Data;

                    // Assert
                    Assert.False(data.success);
                    Assert.Equal("Sorry, 'testUser' hasn't verified their email account yet and we cannot proceed with the request.", data.message);
                }

                public class TheAddPackageOwnerMethods : TestContainer
                {
                    private static readonly IEnumerable<InvokePackageOwnerModificationRequest> _addRequests = new InvokePackageOwnerModificationRequest[]
                    {
                        new InvokePackageOwnerModificationRequest(AddPackageOwner),
                    };

                    public static IEnumerable<object[]> AllAddRequests_Data
                    {
                        get
                        {
                            foreach (var request in _addRequests)
                            {
                                yield return new object[]
                                {
                                    request,
                                };
                            }
                        }
                    }

                    public static IEnumerable<object[]> AllCanManagePackageOwnersByAddRequests_Data
                    {
                        get
                        {
                            foreach (var request in _addRequests)
                            {
                                foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                                {
                                    yield return new object[]
                                    {
                                        request,
                                        canManagePackageOwnersUser,
                                    };
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCanBeAddedByAddRequests_Data
                    {
                        get
                        {
                            foreach (var request in _addRequests)
                            {
                                foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                                {
                                    foreach (var canBeAddedUser in _canBeAddedUsers)
                                    {
                                        yield return new object[]
                                        {
                                            request,
                                            canManagePackageOwnersUser,
                                            canBeAddedUser,
                                        };
                                    };
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCannotBeAddedByAddRequests_Data
                    {
                        get
                        {
                            foreach (var request in _addRequests)
                            {
                                foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                                {
                                    foreach (var cannotBeAddedUser in _cannotBeAddedUsers)
                                    {
                                        yield return new object[]
                                        {
                                            request,
                                            canManagePackageOwnersUser,
                                            cannotBeAddedUser,
                                        };
                                    };
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCanBeAdded_Data
                    {
                        get
                        {
                            foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                            {
                                foreach (var canBeAddedUser in _canBeAddedUsers)
                                {
                                    yield return new object[]
                                    {
                                            canManagePackageOwnersUser,
                                            canBeAddedUser,
                                    };
                                };
                            }
                        }
                    }

                    public static IEnumerable<object[]> PendingOwnerPropagatesPolicy_Data
                    {
                        get
                        {
                            foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                            {
                                foreach (var canBeAddedUser in _canBeAddedUsers)
                                {
                                    foreach (var canBePendingUser in _canBeAddedUsers)
                                    {
                                        if (canBeAddedUser == canBePendingUser)
                                        {
                                            continue;
                                        }

                                        yield return new object[]
                                        {
                                                canManagePackageOwnersUser,
                                                canBeAddedUser,
                                                canBePendingUser,
                                        };
                                    }
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> ReturnsFailureIfCurrentUserNotFoundByAddRequests_Data
                    {
                        get
                        {
                            return AllCanManagePackageOwnersPairedWithCanBeAddedByAddRequests_Data.Where(o => o[1] != o[2]);
                        }
                    }

                    [Theory]
                    [MemberData(nameof(ReturnsFailureIfCurrentUserNotFoundByAddRequests_Data))]
                    public async Task ReturnsFailureIfCurrentUserNotFound(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToAdd)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToAdd = getUserToAdd(fakes);
                        var controller = GetController<JsonApiController>();

                        // Act
                        var result = await request(controller, fakes.Package.Id, userToAdd.Username);
                        dynamic data = ((JsonResult)result).Data;

                        // Assert
                        Assert.False(data.success);
                        Assert.Equal("Current user not found.", data.message);
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCannotBeAddedByAddRequests_Data))]
                    public async Task ReturnsFailureIfNewOwnerIsAlreadyOwner(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToAdd)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToAdd = getUserToAdd(fakes);
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        // Act
                        var result = await request(controller, fakes.Package.Id, userToAdd.Username);
                        dynamic data = ((JsonResult)result).Data;

                        // Assert
                        Assert.False(data.success);
                        Assert.Equal(string.Format(Strings.AddOwner_AlreadyOwner, userToAdd.Username), data.message);
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCanBeAddedByAddRequests_Data))]
                    public async Task ReturnsFailureIfNewOwnerIsAlreadyPending(InvokePackageOwnerModificationRequest request, Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToAdd)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToAdd = getUserToAdd(fakes);
                        var package = fakes.Package;
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        GetMock<IPackageOwnershipManagementService>()
                            .Setup(x => x.GetPackageOwnershipRequests(package, null, userToAdd))
                            .Returns(new PackageOwnerRequest[] { new PackageOwnerRequest() });

                        // Act
                        var result = await request(controller, package.Id, userToAdd.Username);
                        dynamic data = ((JsonResult)result).Data;

                        // Assert
                        Assert.False(data.success);
                        Assert.Equal(string.Format(Strings.AddOwner_AlreadyOwner, userToAdd.Username), data.message);
                    }

                    public class TheAddPackageOwnerMethod : TestContainer
                    {
                        public static IEnumerable<object[]> AllCanManagePackageOwners_Data => ThePackageOwnerMethods.AllCanManagePackageOwners_Data;
                        public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCanBeAdded_Data = TheAddPackageOwnerMethods.AllCanManagePackageOwnersPairedWithCanBeAdded_Data;
                        public static IEnumerable<object[]> PendingOwnerPropagatesPolicy_Data => TheAddPackageOwnerMethods.PendingOwnerPropagatesPolicy_Data;

                        [Theory]
                        [MemberData(nameof(AllCanManagePackageOwners_Data))]
                        public async Task FailsIfUserInputIsEmailAddress(Func<Fakes, User> getCurrentUser)
                        {
                            // Arrange
                            var fakes = Get<Fakes>();
                            var currentUser = getCurrentUser(fakes);
                            var usernameToAdd = "notAUsername@email.com";
                            var package = fakes.Package;
                            var controller = GetController<JsonApiController>();
                            controller.SetCurrentUser(currentUser);
                            AddPackageOwnerViewModel testData = new AddPackageOwnerViewModel
                            {
                                Id = package.Id,
                                Username = usernameToAdd,
                                Message = "a message"
                            };

                            // Act
                            var result = await controller.AddPackageOwner(testData);
                            dynamic data = result.Data;

                            // Assert
                            Assert.False(data.success);
                            Assert.Equal(Strings.AddOwner_NameIsEmail, data.message);
                        }

                        [Theory]
                        [MemberData(nameof(AllCanManagePackageOwnersPairedWithCanBeAdded_Data))]
                        public async Task CreatesPackageOwnerRequestSendsEmailAndReturnsPendingState(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToAdd)
                        {
                            var fakes = Get<Fakes>();

                            var currentUser = getCurrentUser(fakes);
                            var userToAdd = getUserToAdd(fakes);

                            var controller = GetController<JsonApiController>();
                            controller.SetCurrentUser(currentUser);

                            var packageOwnershipManagementServiceMock = GetMock<IPackageOwnershipManagementService>();
                            var messageServiceMock = GetMock<IMessageService>();

                            var pending = !(ActionsRequiringPermissions.HandlePackageOwnershipRequest.CheckPermissions(currentUser, userToAdd) == PermissionsCheckResult.Allowed);

                            if (pending)
                            {
                                packageOwnershipManagementServiceMock
                                    .Setup(p => p.AddPackageOwnershipRequestAsync(fakes.Package, currentUser, userToAdd))
                                    .Returns(Task.FromResult(new PackageOwnerRequest { ConfirmationCode = "confirmation-code" }))
                                    .Verifiable();

                                messageServiceMock
                                    .Setup(m => m.SendMessageAsync(It.IsAny<PackageOwnershipRequestMessage>(), false, false))
                                    .Callback<IEmailBuilder, bool, bool>((msg, copySender, discloseSenderAddress) =>
                                    {
                                        var message = msg as PackageOwnershipRequestMessage;
                                        Assert.Equal(currentUser, message.FromUser);
                                        Assert.Equal(userToAdd, message.ToUser);
                                        Assert.Equal(fakes.Package, message.PackageRegistration);
                                        Assert.Equal(TestUtility.GallerySiteRootHttps + "packages/FakePackage/", message.PackageUrl);
                                        Assert.Equal(TestUtility.GallerySiteRootHttps + $"packages/FakePackage/owners/{userToAdd.Username}/confirm/confirmation-code", message.ConfirmationUrl);
                                        Assert.Equal(TestUtility.GallerySiteRootHttps + $"packages/FakePackage/owners/{userToAdd.Username}/reject/confirmation-code", message.RejectionUrl);
                                        Assert.Equal("Hello World! Html Encoded &lt;3", message.HtmlEncodedMessage);
                                        Assert.Equal(string.Empty, message.PolicyMessage);
                                    })
                                    .Returns(Task.CompletedTask)
                                    .Verifiable();

                                foreach (var owner in fakes.Package.Owners)
                                {
                                    messageServiceMock
                                          .Setup(m => m.SendMessageAsync(
                                              It.Is<PackageOwnershipRequestInitiatedMessage>(
                                                  msg =>
                                                  msg.RequestingOwner == currentUser
                                                  && msg.ReceivingOwner == owner
                                                  && msg.NewOwner == userToAdd
                                                  && msg.PackageRegistration == fakes.Package),
                                              false,
                                              false))
                                          .Returns(Task.CompletedTask)
                                          .Verifiable();
                                }
                            }
                            else
                            {
                                packageOwnershipManagementServiceMock
                                    .Setup(p => p.AddPackageOwnerAsync(fakes.Package, userToAdd, true))
                                    .Returns(Task.CompletedTask)
                                    .Verifiable();

                                foreach (var owner in fakes.Package.Owners)
                                {
                                    messageServiceMock
                                        .Setup(m => m.SendMessageAsync(
                                            It.Is<PackageOwnerAddedMessage>(
                                                msg =>
                                                msg.ToUser == owner
                                                && msg.NewOwner == userToAdd
                                                && msg.PackageRegistration == fakes.Package),
                                            false,
                                            false))
                                        .Returns(Task.CompletedTask)
                                        .Verifiable();
                                }
                            }
                            AddPackageOwnerViewModel testData = new AddPackageOwnerViewModel
                            {
                                Id = fakes.Package.Id,
                                Username = userToAdd.Username,
                                Message = "Hello World! Html Encoded <3"
                            };


                            JsonResult result = await controller.AddPackageOwner(testData);
                            dynamic data = result.Data;
                            PackageOwnersResultViewModel model = data.model;

                            Assert.True(data.success);
                            Assert.Equal(userToAdd.Username, model.Name);
                            Assert.Equal(pending, model.Pending);

                            packageOwnershipManagementServiceMock.Verify();
                            messageServiceMock.Verify();
                        }
                    }
                }

                public class TheRemovePackageOwnerMethod : TestContainer
                {
                    private static readonly IEnumerable<Func<Fakes, User>> _canBeRemovedUsers = _cannotBeAddedUsers;

                    private static readonly IEnumerable<Func<Fakes, User>> _cannotBeRemovedUsers = _canBeAddedUsers;

                    public static IEnumerable<object[]> AllCanManagePackageOwners_Data => ThePackageOwnerMethods.AllCanManagePackageOwners_Data;

                    public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCanBeRemoved_Data
                    {
                        get
                        {
                            foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                            {
                                foreach (var canBeRemovedUser in _canBeRemovedUsers)
                                {
                                    yield return new object[]
                                    {
                                        canManagePackageOwnersUser,
                                        canBeRemovedUser,
                                    };
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> AllCanManagePackageOwnersPairedWithCannotBeRemoved_Data
                    {
                        get
                        {
                            foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                            {
                                foreach (var cannotBeRemovedUser in _cannotBeRemovedUsers)
                                {
                                    yield return new object[]
                                    {
                                        canManagePackageOwnersUser,
                                        cannotBeRemovedUser,
                                    };
                                }
                            }
                        }
                    }

                    public static IEnumerable<object[]> ReturnsFailureIfCurrentUserNotFound_Data
                    {
                        get
                        {
                            return AllCanManagePackageOwnersPairedWithCanBeRemoved_Data.Where(o => o[0] != o[1]);
                        }
                    }

                    [Theory]
                    [MemberData(nameof(ReturnsFailureIfCurrentUserNotFound_Data))]
                    public async Task ReturnsFailureIfCurrentUserNotFound(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToRemove)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToRemove = getUserToRemove(fakes);
                        var controller = GetController<JsonApiController>();

                        // Act
                        var result = await controller.RemovePackageOwner(fakes.Package.Id, userToRemove.Username);
                        dynamic data = result.Data;

                        // Assert
                        Assert.False(data.success);
                        Assert.Equal("Current user not found.", data.message);
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCannotBeRemoved_Data))]
                    public async Task ReturnsFailureIfUserIsNotAnOwner(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToRemove)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToRemove = getUserToRemove(fakes);
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        // Act
                        var result = await controller.RemovePackageOwner(fakes.Package.Id, userToRemove.Username);
                        dynamic data = result.Data;

                        // Assert
                        Assert.False(data.success);
                        Assert.Equal(string.Format(Strings.RemoveOwner_NotOwner, userToRemove.Username), data.message);
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCannotBeRemoved_Data))]
                    public async Task RemovesPackageOwnerRequest(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getPendingUser)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var requestedUser = getPendingUser(fakes);
                        var package = fakes.Package;
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        var packageOwnershipManagementService = GetMock<IPackageOwnershipManagementService>();

                        packageOwnershipManagementService
                            .Setup(x => x.GetPackageOwnershipRequests(package, null, requestedUser))
                            .Returns(new PackageOwnerRequest[] { new PackageOwnerRequest() });

                        // Act
                        var result = await controller.RemovePackageOwner(package.Id, requestedUser.Username);
                        dynamic data = result.Data;

                        // Assert
                        Assert.True(data.success);

                        packageOwnershipManagementService.Verify(x => x.DeletePackageOwnershipRequestAsync(package, requestedUser, true));

                        GetMock<IMessageService>()
                            .Verify(x => x.SendMessageAsync(
                                It.Is<PackageOwnershipRequestCanceledMessage>(
                                    msg =>
                                    msg.RequestingOwner == currentUser
                                    && msg.NewOwner == requestedUser
                                    && msg.PackageRegistration == package),
                                false,
                                false));
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCanBeRemoved_Data))]
                    public async Task FailsIfAttemptingToRemoveLastOwner(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToRemove)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToRemove = getUserToRemove(fakes);
                        var package = fakes.Package;
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        foreach (var owner in package.Owners.Where(o => !o.MatchesUser(userToRemove)).ToList())
                        {
                            package.Owners.Remove(owner);
                        }

                        var packageOwnershipManagementService = GetMock<IPackageOwnershipManagementService>();

                        packageOwnershipManagementService
                            .Setup(x => x.GetPackageOwnershipRequests(package, null, userToRemove))
                            .Returns(Enumerable.Empty<PackageOwnerRequest>());

                        // Act
                        var result = await controller.RemovePackageOwner(package.Id, userToRemove.Username);
                        dynamic data = result.Data;

                        // Assert
                        Assert.False(data.success);

                        packageOwnershipManagementService
                            .Verify(
                                x => x.RemovePackageOwnerAsync(package, currentUser, userToRemove, It.IsAny<bool>()),
                                Times.Never());

                        GetMock<IMessageService>()
                            .Verify(
                                x => x.SendMessageAsync(
                                    It.Is<PackageOwnerRemovedMessage>(
                                        msg =>
                                        msg.FromUser == currentUser
                                        && msg.ToUser == userToRemove
                                        && msg.PackageRegistration == package),
                                    false,
                                    false),
                                Times.Never());
                    }

                    [Theory]
                    [MemberData(nameof(AllCanManagePackageOwnersPairedWithCanBeRemoved_Data))]
                    public async Task RemovesExistingOwner(Func<Fakes, User> getCurrentUser, Func<Fakes, User> getUserToRemove)
                    {
                        // Arrange
                        var fakes = Get<Fakes>();
                        var currentUser = getCurrentUser(fakes);
                        var userToRemove = getUserToRemove(fakes);
                        var package = fakes.Package;
                        var controller = GetController<JsonApiController>();
                        controller.SetCurrentUser(currentUser);

                        var packageOwnershipManagementService = GetMock<IPackageOwnershipManagementService>();

                        packageOwnershipManagementService
                            .Setup(x => x.GetPackageOwnershipRequests(package, null, userToRemove))
                            .Returns(Enumerable.Empty<PackageOwnerRequest>());

                        // Act
                        var result = await controller.RemovePackageOwner(package.Id, userToRemove.Username);
                        dynamic data = result.Data;

                        // Assert
                        Assert.True(data.success);

                        packageOwnershipManagementService.Verify(x => x.RemovePackageOwnerAsync(package, currentUser, userToRemove, It.IsAny<bool>()));

                        GetMock<IMessageService>()
                            .Verify(x => x.SendMessageAsync(
                                It.Is<PackageOwnerRemovedMessage>(
                                    msg =>
                                    msg.FromUser == currentUser
                                    && msg.ToUser == userToRemove
                                    && msg.PackageRegistration == package),
                                false,
                                false));
                    }
                }

                public delegate Task<ActionResult> InvokePackageOwnerModificationRequest(JsonApiController jsonApiController, string packageId, string username);

                private static async Task<ActionResult> AddPackageOwner(JsonApiController jsonApiController, string packageId, string username)
                {
                    AddPackageOwnerViewModel testData = new AddPackageOwnerViewModel
                    {
                        Id = packageId,
                        Username = username,
                        Message = "message"
                    };

                    return await jsonApiController.AddPackageOwner(testData);
                }

                private static async Task<ActionResult> RemovePackageOwner(JsonApiController jsonApiController, string packageId, string username)
                {
                    return await jsonApiController.RemovePackageOwner(packageId, username);
                }

                private static readonly IEnumerable<InvokePackageOwnerModificationRequest> _requests = new InvokePackageOwnerModificationRequest[]
                {
                    new InvokePackageOwnerModificationRequest(AddPackageOwner),
                    new InvokePackageOwnerModificationRequest(RemovePackageOwner),
                };

                public static IEnumerable<object[]> AllRequests_Data
                {
                    get
                    {
                        foreach (var request in _requests)
                        {
                            yield return new object[]
                            {
                                request
                            };
                        }
                    }
                }

                public static IEnumerable<object[]> AllCanManagePackageOwnersByRequests_Data
                {
                    get
                    {
                        foreach (var request in _requests)
                        {
                            foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                            {
                                yield return new object[]
                                {
                                    request,
                                    canManagePackageOwnersUser,
                                };
                            }
                        }
                    }
                }

                public static IEnumerable<object[]> AllCannotManagePackageOwnersByRequests_Data
                {
                    get
                    {
                        foreach (var request in _requests)
                        {
                            foreach (var cannotManagePackageOwnersUser in _cannotManagePackageOwnersUsers)
                            {
                                yield return new object[]
                                {
                                    request,
                                    cannotManagePackageOwnersUser,
                                };
                            }
                        }
                    }
                }
            }

            public class TheGetPackageOwnersMethod : TestContainer
            {
                public static IEnumerable<object[]> AllCanManagePackageOwners_Data => ThePackageOwnerMethods.AllCanManagePackageOwners_Data;

                public static IEnumerable<object[]> AllCannotManagePackageOwners_Data => ThePackageOwnerMethods.AllCannotManagePackageOwners_Data;

                [Fact]
                public void ReturnsFailureIfPackageNotFound()
                {
                    // Arrange
                    var controller = GetController<JsonApiController>();

                    // Act
                    var result = controller.GetPackageOwners("fakeId");
                    dynamic data = ((JsonResult)result).Data;

                    // Assert
                    Assert.Equal("Package not found.", data.message);
                }

                [Theory]
                [MemberData(nameof(AllCannotManagePackageOwners_Data))]
                public void ReturnsFailureIfUserIsNotPackageOwner(Func<Fakes, User> getCurrentUser)
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    var currentUser = getCurrentUser(fakes);
                    var controller = GetController<JsonApiController>();
                    controller.SetCurrentUser(currentUser);

                    // Act
                    var result = controller.GetPackageOwners(fakes.Package.Id);

                    // Assert
                    Assert.IsType<HttpUnauthorizedResult>(result);
                }

                [Fact]
                public void ReturnsExpectedDataAsOwner()
                {
                    var fakes = Get<Fakes>();
                    var currentUser = fakes.Owner;
                    var result = InvokeAsUser(currentUser);

                    Assert.Contains(result, m => ModelMatchesUser(m, fakes.Owner, grantsCurrentUserAccess: true, isCurrentUserAdminOfOrganization: false));
                    Assert.Contains(result, m => ModelMatchesUser(m, fakes.OrganizationOwner, grantsCurrentUserAccess: false, isCurrentUserAdminOfOrganization: false));
                }

                [Fact]
                public void ReturnsExpectedDataAsOrganizationAdmin()
                {
                    var fakes = Get<Fakes>();
                    var currentUser = fakes.OrganizationOwnerAdmin;
                    var result = InvokeAsUser(currentUser);

                    Assert.Contains(result, m => ModelMatchesUser(m, fakes.Owner, grantsCurrentUserAccess: false, isCurrentUserAdminOfOrganization: false));
                    Assert.Contains(result, m => ModelMatchesUser(m, fakes.OrganizationOwner, grantsCurrentUserAccess: true, isCurrentUserAdminOfOrganization: true));
                }

                [Fact]
                public void UsesGravatarIfProxyingDisabled()
                {
                    GetMock<IFeatureFlagService>()
                        .Setup(f => f.IsGravatarProxyEnabled())
                        .Returns(false);

                    var fakes = Get<Fakes>();
                    var currentUser = fakes.Owner;
                    var result = InvokeAsUser(currentUser).ToList();

                    Assert.Equal(2, result.Count);
                    Assert.Equal(
                        "https://secure.gravatar.com/avatar/f97acb220d5765fc9d56e45f826b9fc2?s=64&r=g&d=retro",
                        result[0].ImageUrl);
                    Assert.Equal(
                        "https://secure.gravatar.com/avatar/5ed91983fd0fc9a6df3d7c1a2a050290?s=64&r=g&d=retro",
                        result[1].ImageUrl);
                }

                [Fact]
                public void ProxiesGravatar()
                {
                    GetMock<IFeatureFlagService>()
                        .Setup(f => f.IsGravatarProxyEnabled())
                        .Returns(true);

                    var fakes = Get<Fakes>();
                    var currentUser = fakes.Owner;
                    var result = InvokeAsUser(currentUser).ToList();

                    Assert.Equal(2, result.Count);
                    Assert.Equal("/profiles/testPackageOwner/avatar?imageSize=64", result[0].ImageUrl);
                    Assert.Equal("/profiles/testOrganizationOwner/avatar?imageSize=64", result[1].ImageUrl);
                }

                private IEnumerable<PackageOwnersResultViewModel> InvokeAsUser(User currentUser)
                {
                    var controller = GetController<JsonApiController>();
                    controller.SetCurrentUser(currentUser);

                    var result = controller.GetPackageOwners("FakePackage");
                    return ((JsonResult)result).Data as IEnumerable<PackageOwnersResultViewModel>;
                }

                private bool ModelMatchesUser(PackageOwnersResultViewModel model, User user, bool grantsCurrentUserAccess, bool isCurrentUserAdminOfOrganization)
                {
                    return
                        user.Username == model.Name &&
                        user.EmailAddress == model.EmailAddress &&
                        grantsCurrentUserAccess == model.GrantsCurrentUserAccess &&
                        isCurrentUserAdminOfOrganization == model.IsCurrentUserAdminOfOrganization;
                }
            }

            private static readonly Func<Fakes, User> _getFakesNull = (Fakes fakes) => null;
            private static readonly Func<Fakes, User> _getFakesUser = (Fakes fakes) => fakes.User;
            private static readonly Func<Fakes, User> _getFakesOwner = (Fakes fakes) => fakes.Owner;
            private static readonly Func<Fakes, User> _getFakesOrganizationOwner = (Fakes fakes) => fakes.OrganizationOwner;
            private static readonly Func<Fakes, User> _getFakesOrganizationAdminOwner = (Fakes fakes) => fakes.OrganizationOwnerAdmin;
            private static readonly Func<Fakes, User> _getFakesOrganizationCollaboratorOwner = (Fakes fakes) => fakes.OrganizationOwnerCollaborator;

            public static IEnumerable<string> _missingData = new[] { null, string.Empty };

            private static readonly IEnumerable<Func<Fakes, User>> _canManagePackageOwnersUsers = new Func<Fakes, User>[]
            {
                _getFakesOwner,
                _getFakesOrganizationOwner,
                _getFakesOrganizationAdminOwner,
            };

            private static readonly IEnumerable<Func<Fakes, User>> _cannotManagePackageOwnersUsers = new Func<Fakes, User>[]
            {
                _getFakesNull,
                _getFakesUser,
                _getFakesOrganizationCollaboratorOwner,
            };

            private static readonly IEnumerable<Func<Fakes, User>> _canBeAddedUsers = new Func<Fakes, User>[]
            {
                _getFakesUser,
                _getFakesOrganizationAdminOwner,
                _getFakesOrganizationCollaboratorOwner,
            };

            private static readonly IEnumerable<Func<Fakes, User>> _cannotBeAddedUsers = new Func<Fakes, User>[]
            {
                _getFakesOwner,
                _getFakesOrganizationOwner,
            };

            public static IEnumerable<object[]> AllCanManagePackageOwners_Data
            {
                get
                {
                    foreach (var canManagePackageOwnersUser in _canManagePackageOwnersUsers)
                    {
                        yield return new object[]
                        {
                            canManagePackageOwnersUser,
                        };
                    }
                }
            }

            public static IEnumerable<object[]> AllCannotManagePackageOwners_Data
            {
                get
                {
                    foreach (var cannotManagePackageOwnersUser in _cannotManagePackageOwnersUsers)
                    {
                        yield return new object[]
                        {
                            cannotManagePackageOwnersUser,
                        };
                    }
                }
            }
        }
    }
}