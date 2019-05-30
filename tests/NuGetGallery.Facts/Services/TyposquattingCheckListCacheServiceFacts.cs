// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingCheckListCacheServiceFacts
    {
        private static List<string> _packageIds = new List<string>
        {
            "microsoft_netframework_v1",
            "WindowsAzure.Caching",
            "SinglePageApplication",
            "PoliteCaptcha",
            "AspNetRazor.Core",
            "System.Json",
            "System.Spatial"
        };

        private static IQueryable<PackageRegistration> PacakgeRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
                new PackageRegistration()
                {
                    Id = _packageIds[i],
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", i + 1), Key = i + 1 } }
                }).AsQueryable();

        [Fact]
        public void WhenGettingCheckListFromMultipleThreadsItIsInitializedOnce()
        {
            // Arrange
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);

            var newService = new TyposquattingCheckListCacheService();

            // Act
            int tasksNum = 3;
            Task[] tasks = new Task[tasksNum];
            for (int i = 0; i < tasksNum; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(24), mockPackageService.Object);
                    newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(24), mockPackageService.Object);
                });
            }
            Task.WaitAll(tasks);

            // Assert
            mockPackageService.Verify(
               x => x.GetAllPackageRegistrations(),
               Times.Once);
        }

        [Fact]
        public void WhenExceedExpireTimeRefreshCheckListCache()
        {
            // Arrange
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);

            var newService = new TyposquattingCheckListCacheService();

            // Act
            newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(0), mockPackageService.Object);
            Thread.Sleep(1);
            newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(0), mockPackageService.Object);

            // Assert
            mockPackageService.Verify(
               x => x.GetAllPackageRegistrations(),
               Times.Exactly(2));
        }

        [Fact]
        public void WhenNotEqualConfiguredLengthRefreshCheckListCache()
        {
            // Arrange
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);

            var newService = new TyposquattingCheckListCacheService();

            // Act
            newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(24), mockPackageService.Object);
            newService.GetTyposquattingCheckList(_packageIds.Count - 1, TimeSpan.FromHours(24), mockPackageService.Object);

            // Assert
            mockPackageService.Verify(
               x => x.GetAllPackageRegistrations(),
               Times.Exactly(2));
        }

        [Fact]
        public void CheckConfiguredLengthNotAllowed()
        {
            // Arrange
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);
            var checkListConfiguredLength = -1;

            var newService = new TyposquattingCheckListCacheService();

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => newService.GetTyposquattingCheckList(checkListConfiguredLength, TimeSpan.FromHours(24), mockPackageService.Object));

            // Assert
            Assert.Equal(nameof(checkListConfiguredLength), exception.ParamName);
        }

        [Fact]
        public void CheckExpireTimeNotAllowed()
        {
            // Arrange
            var checkListExpireTime = TimeSpan.FromHours(-1);
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);

            var newService = new TyposquattingCheckListCacheService();

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => newService.GetTyposquattingCheckList(_packageIds.Count, checkListExpireTime, mockPackageService.Object));

            // Assert
            Assert.Equal(nameof(checkListExpireTime), exception.ParamName);
        }

        [Fact]
        public void CheckNullPackageService()
        {
            // Arrange
            var newService = new TyposquattingCheckListCacheService();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.GetTyposquattingCheckList(_packageIds.Count, TimeSpan.FromHours(24), null));

            // Assert
            Assert.Equal("packageService", exception.ParamName);
        }
    }
}