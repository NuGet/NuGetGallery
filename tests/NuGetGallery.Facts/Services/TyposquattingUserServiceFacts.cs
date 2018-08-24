// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingUserServiceFacts
    {
        private static Mock<IPackageService> _packageService = new Mock<IPackageService>();

        [Fact]
        public void CheckTrueTyposquattingUserService()
        {
            // Arrange            
            var package = new PackageRegistration();
            var user = new User();
            user.Username = "owner1";
            package.Owners.Add(user);

            var packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            var newService = new TyposquattingUserService(_packageService.Object);
            
            // Act
            var ownersDoubleCheckResult = newService.CanUserTyposquat(packageId, "owner1");

            // Assert
            Assert.True(ownersDoubleCheckResult);
        }

        [Fact]
        public void CheckFalseTyposquattingUserService()
        {
            // Arrange            
            var package = new PackageRegistration();
            var user = new User();
            user.Username = "owner2";
            
            var packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            var newService = new TyposquattingUserService(_packageService.Object);

            // Act
            var ownersDoubleCheckResult = newService.CanUserTyposquat(packageId, "owner1");

            // Assert
            Assert.False(ownersDoubleCheckResult);
        }

        [Fact]
        public void CheckNullTyposquattingUserService()
        {
            // Arrange  
            PackageRegistration package = null;
            var packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            var newService = new TyposquattingUserService(_packageService.Object);

            // Act
            var ownersDoubleCheckResult = newService.CanUserTyposquat(packageId, "owner1");

            // Assert
            Assert.False(ownersDoubleCheckResult);
        }
    }
}
