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
            PackageRegistration package = new PackageRegistration();
            User user = new User();
            user.Username = "owner1";
            package.Owners.Add(user);

            string packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            TyposquattingUserService newService = new TyposquattingUserService(_packageService.Object);
            
            // Act
            var ownersDoubleCheckResult = newService.CanUserTyposquat(packageId, "owner1");

            // Assert
            Assert.True(ownersDoubleCheckResult);
        }

        [Fact]
        public void CheckFalseTyposquattingUserService()
        {
            // Arrange            
            PackageRegistration package = new PackageRegistration();
            User user = new User();
            user.Username = "owner2";
            
            string packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            TyposquattingUserService newService = new TyposquattingUserService(_packageService.Object);

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
            string packageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(package);

            TyposquattingUserService newService = new TyposquattingUserService(_packageService.Object);

            // Act
            var ownersDoubleCheckResult = newService.CanUserTyposquat(packageId, "owner1");

            // Assert
            Assert.True(ownersDoubleCheckResult);
        }
    }
}
