// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
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
                Assert.Throws<ArgumentNullException>(() => CreateService().IsValidPackageOwnerRequest(null, new User(), "token"));
            }

            [Fact]
            public void ThrowsArgumentNullIfPendingOwnerIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => CreateService().IsValidPackageOwnerRequest(new PackageRegistration(), null, "token"));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void ThrowsArgumentNullIfTokenIsNullOrEmpty(string token)
            {
                Assert.Throws<ArgumentNullException>(() => CreateService().IsValidPackageOwnerRequest(new PackageRegistration(), new User(), token));
            }

            [Theory]
            [InlineData(1, 2, "token", true)]
            [InlineData(2, 2, "token", false)]
            [InlineData(1, 1, "token", false)]
            [InlineData(1, 2, "token2", false)]
            private void ReturnsSuccessIfPackageOwnerRequestMatches(int packageId, int userId, string token, bool success)
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
                var isValid = service.IsValidPackageOwnerRequest(package, pendingOwner, token);

                // Assert
                Assert.Equal(success, isValid);
            }
        }

        public class TheAddPackageOwnershipRequestMethod
        {
            [Fact]
            public async Task CreatesPackageOwnerRequest()
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);
                var package = new PackageRegistration { Key = 1 };
                var owner = new User { Key = 100 };
                var newOwner = new User { Key = 200 };

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
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);

                // Act
                var request = await service.AddPackageOwnershipRequest(package, newRequestingOwner, newOwner);

                // Assert
                Assert.Equal(existingRequestingOwner.Key, request.RequestingOwnerKey);
            }
        }

        private static IPackageOwnerRequestService CreateService(Mock<IEntityRepository<PackageOwnerRequest>> packageOwnerRequestRepo = null)
        {
            packageOwnerRequestRepo = packageOwnerRequestRepo ?? new Mock<IEntityRepository<PackageOwnerRequest>>();

            return new PackageOwnerRequestService(packageOwnerRequestRepo.Object);
        }
    }
}
