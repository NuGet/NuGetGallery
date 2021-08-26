// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageOwnerRequestServiceFacts
    {
        public class TheIsValidPackageOwnerRequestMethod
        {
            [Fact]
            public void ThrowsArgumentNullIfPackageIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => CreateService().GetPackageOwnershipRequest(null, new User(), "token"));
            }

            [Fact]
            public void ThrowsArgumentNullIfPendingOwnerIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => CreateService().GetPackageOwnershipRequest(new PackageRegistration(), null, "token"));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void ThrowsArgumentNullIfTokenIsNullOrEmpty(string token)
            {
                Assert.Throws<ArgumentNullException>(() => CreateService().GetPackageOwnershipRequest(new PackageRegistration(), new User(), token));
            }

            [Theory]
            [InlineData(1, 2, "token", true)]
            [InlineData(2, 2, "token", false)]
            [InlineData(1, 1, "token", false)]
            [InlineData(1, 2, "token2", false)]
            public void ReturnsSuccessIfPackageOwnerRequestMatches(int packageId, int userId, string token, bool success)
            {
                // Arrange
                const int actualKey = 1;
                const int actualNewOwner = 2;
                const string actualToken = "token";

                var package = new PackageRegistration { Key = packageId };
                var pendingOwner = new User { Key = userId };

                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                    {
                        new PackageOwnerRequest
                        {
                            PackageRegistration = new PackageRegistration { Key = actualKey },
                            PackageRegistrationKey = actualKey,

                            NewOwner = new User { Key = actualNewOwner },
                            NewOwnerKey = actualNewOwner,

                            ConfirmationCode = actualToken
                        }
                    }
                    .AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                // Act
                var request = service.GetPackageOwnershipRequest(package, pendingOwner, token);

                // Assert
                if (success)
                {
                    Assert.NotNull(request);
                }
                else
                {
                    Assert.Null(request);
                }
            }
        }

        public class TheAddPackageOwnershipRequestMethod
        {
            [Fact]
            public async Task NullPackageRegistrationThrowsException()
            {
                var service = CreateService();
                var user1 = new User { Key = 100, Username = "user1" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequest(package: null, requestingOwner: user1, newOwner: user2));
            }

            [Fact]
            public async Task NullRequestingUserThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user2 = new User { Key = 101, Username = "user2" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequest(package: package, requestingOwner: null, newOwner: user2));
            }

            [Fact]
            public async Task NullNewOwnerThrowsException()
            {
                var service = CreateService();
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var user1 = new User { Key = 101, Username = "user1" };
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.AddPackageOwnershipRequest(package: package, requestingOwner: user1, newOwner: null));
            }

            [Fact]
            public async Task CreatesPackageOwnerRequest()
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                var service = CreateService(packageOwnerRequestRepository);
                var package = new PackageRegistration { Id = "NuGet.Versioning", Key = 1 };
                var owner = new User { Username = "NuGet", Key = 100 };
                var newOwner = new User { Username = "Microsoft", Key = 200 };

                await service.AddPackageOwnershipRequest(package, owner, newOwner);

                packageOwnerRequestRepository.Verify(
                    r => r.InsertOnCommit(
                        It.Is<PackageOwnerRequest>(req => req.PackageRegistrationKey == 1 && req.RequestingOwnerKey == 100 && req.NewOwnerKey == 200))
                    );
            }

            [Fact]
            public async Task ReturnsExistingMatchingPackageOwnerRequest()
            {
                // Arrange
                var package = new PackageRegistration { Key = 1 };
                var existingRequestingOwner = new User { Key = 100 };
                var newRequestingOwner = new User { Key = 99 };
                var newOwner = new User { Key = 200 };

                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                packageOwnerRequestRepository.Setup(r => r.GetAll()).Returns(
                    new[]
                    {
                        new PackageOwnerRequest
                        {
                            PackageRegistration = package,
                            PackageRegistrationKey = package.Key,

                            RequestingOwner = existingRequestingOwner,
                            RequestingOwnerKey = existingRequestingOwner.Key,

                            NewOwner = newOwner,
                            NewOwnerKey = newOwner.Key
                        }
                    }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepository);

                // Act
                var request = await service.AddPackageOwnershipRequest(package, newRequestingOwner, newOwner);

                // Assert
                Assert.Equal(existingRequestingOwner.Key, request.RequestingOwnerKey);
            }
        }

        public class TheDeletePackageOwnershipRequestMethod
        {
            [Fact]
            public async Task NullRequestThrowsException()
            {
                var service = CreateService();
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.DeletePackageOwnershipRequest(request: null));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DeletesPackageOwnerRequest(bool commitChanges)
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                var service = CreateService(packageOwnerRequestRepository);
                var request = new PackageOwnerRequest
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.Versioning" },
                    RequestingOwner = new User { Username = "NuGet" },
                    NewOwner = new User { Username = "Microsoft" },
                };

                await service.DeletePackageOwnershipRequest(request, commitChanges);

                packageOwnerRequestRepository.Verify(r => r.DeleteOnCommit(request), Times.Once);
                packageOwnerRequestRepository.Verify(r => r.CommitChangesAsync(), commitChanges ? Times.Once() : Times.Never());
            }
        }

        private static IPackageOwnerRequestService CreateService(
            Mock<IEntityRepository<PackageOwnerRequest>> packageOwnerRequestRepo = null)
        {
            packageOwnerRequestRepo = packageOwnerRequestRepo ?? new Mock<IEntityRepository<PackageOwnerRequest>>();

            return new PackageOwnerRequestService(packageOwnerRequestRepo.Object);
        }
    }
}
