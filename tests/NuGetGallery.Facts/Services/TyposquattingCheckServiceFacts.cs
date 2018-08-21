// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingCheckServiceFacts
    {
        private static List<ThresholdInfo> _thresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo { LowerBound = 0, UpperBound = 30, Threshold = 0 },
            new ThresholdInfo { LowerBound = 30, UpperBound = 50, Threshold = 1 },
            new ThresholdInfo { LowerBound = 50, UpperBound = 120, Threshold = 2 }
        };

        private static List<PackageInfo> _checkList = new List<PackageInfo>
        {
            new PackageInfo { Id = "microsoft_netframework_v1", Owners = new HashSet<string> { "owner1" } },
            new PackageInfo { Id = "resxtocs_core", Owners = new HashSet<string> { "owner2" } },
            new PackageInfo { Id = "gisrestapi", Owners = new HashSet<string> { "owner3" } },
            new PackageInfo { Id = "xamarinfirebase", Owners = new HashSet<string> { "owner4" } },
            new PackageInfo { Id = "shsoft_infrastructure", Owners = new HashSet<string> { "owner5" } },
            new PackageInfo { Id = "telegram_net_core", Owners = new HashSet<string> { "owner6" } },
            new PackageInfo { Id = "selenium_webDriver_microsoftdriver", Owners = new HashSet<string> { "owner7" } }
        };

        private static Mock<ITyposquattingUserService> _typosquattingOwnersDoubleCheck = new Mock<ITyposquattingUserService>();

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var owner = new User();
            var uploadedPackageId = "new_package_for_testing";
            _typosquattingOwnersDoubleCheck
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            var newService = new TyposquattingCheckService(_typosquattingOwnersDoubleCheck.Object);
            TyposquattingCheckService.PackagesCheckList = _checkList;

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckNotTyposquattingBySameOwnersTest()
        {
            // Arrange            
            var owner = new User();
            owner.Username = "owner1";
            var uploadedPackageId = "microsoft_netframework.v1";
            _typosquattingOwnersDoubleCheck
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            var newService = new TyposquattingCheckService(_typosquattingOwnersDoubleCheck.Object);
            TyposquattingCheckService.PackagesCheckList = _checkList;

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersThroughDoubleCheckTest()
        {
            // Arrange            
            var owner = new User();
            owner.Username = "newOwner1";
            var uploadedPackageId = "Microsoft_NetFramework.v1";

            _typosquattingOwnersDoubleCheck
                .Setup(x => x.CanUserTyposquat("microsoft_netframework_v1", "newOwner1"))
                .Returns(true);
            var newService = new TyposquattingCheckService(_typosquattingOwnersDoubleCheck.Object);
            TyposquattingCheckService.PackagesCheckList = _checkList;

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var owner = new User();
            var uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";
            _typosquattingOwnersDoubleCheck
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            var newService = new TyposquattingCheckService(_typosquattingOwnersDoubleCheck.Object);
            TyposquattingCheckService.PackagesCheckList = _checkList;

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.True(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingNulluploadedPackageId()
        {
            // Arrange            
            var owner = new User();
            string uploadedPackageId = null;
            _typosquattingOwnersDoubleCheck
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            var newService = new TyposquattingCheckService(_typosquattingOwnersDoubleCheck.Object);
            TyposquattingCheckService.PackagesCheckList = _checkList;

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner));

            // Assert
            Assert.Equal(nameof(uploadedPackageId), exception.ParamName);
        }
    }
}