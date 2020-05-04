// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using NuGet.Services.Entities;
using NuGetGallery.Framework;

namespace NuGetGallery.Services
{
    public class PackageRenameServiceFacts
    {
        public class TheGetDeprecationByPackageMethod : TestContainer
        {
            [Fact]
            public void GetPackageRenamesGivenPackageRegistration()
            {
                // Arrange
                var packageRegistration1 = new PackageRegistration
                {
                    Id = "PR1",
                    Key = 1
                };
                var packageRegistration2 = new PackageRegistration
                {
                    Id = "PR2",
                    Key = 2
                };
                var packageRegistration3 = new PackageRegistration
                {
                    Id = "PR3",
                    Key = 3
                };

                var packageRename1 = new PackageRename
                {
                    FromPackageRegistrationKey = 1,
                    FromPackageRegistration = packageRegistration1,
                    ToPackageRegistrationKey = 2,
                    ToPackageRegistration = packageRegistration2
                };
                var packageRename2 = new PackageRename
                {
                    FromPackageRegistrationKey = 1,
                    FromPackageRegistration = packageRegistration1,
                    ToPackageRegistrationKey = 3,
                    ToPackageRegistration = packageRegistration3
                };
                var packageRename3 = new PackageRename
                {
                    FromPackageRegistrationKey = 2,
                    FromPackageRegistration = packageRegistration2,
                    ToPackageRegistrationKey = 3,
                    ToPackageRegistration = packageRegistration3
                };

                var context = GetFakeContext();
                context.PackageRenames.AddRange(
                    new[] { packageRename1, packageRename2, packageRename3 });

                var target = Get<PackageRenameService>();

                // Act and Assert
                var result = target.GetPackageRenames(packageRegistration1);
                Assert.Equal(2, result.Count);
                Assert.Equal(2, result[0].ToPackageRegistrationKey);
                Assert.Equal("PR2", result[0].ToPackageRegistration.Id);
                Assert.Equal(3, result[1].ToPackageRegistrationKey);
                Assert.Equal("PR3", result[1].ToPackageRegistration.Id);

                // Act and Assert
                var result2 = target.GetPackageRenames(packageRegistration2);
                Assert.Equal(1, result2.Count);
                Assert.Equal(3, result2[0].ToPackageRegistrationKey);
                Assert.Equal("PR3", result2[0].ToPackageRegistration.Id);

                // Act and Assert
                var result3 = target.GetPackageRenames(packageRegistration3);
                Assert.Equal(0, result3.Count);
            }
        }
    }
}