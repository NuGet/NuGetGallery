// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    internal static class TestUtility
    {
        internal static void RequireUnsignedPackage(
            Mock<ICorePackageService> corePackageService,
            string packageId,
            string packageVersion,
            PackageStatus status = PackageStatus.Validating)
        {
            var packageRegistration = new PackageRegistration
            {
                Key = 1,
                Id = packageId
            };
            var package = new Package
            {
                PackageStatusKey = status
            };
            var user = new User
            {
                Key = 2
            };

            packageRegistration.Owners.Add(user);

            corePackageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(packageRegistration);

            corePackageService
                .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, packageVersion))
                .Returns(package);
        }

        internal static void RequireSignedPackage(
            Mock<ICorePackageService> corePackageService,
            string packageId,
            string packageVersion,
            string thumbprint = null,
            PackageStatus status = PackageStatus.Validating)
        {
            var packageRegistration = new PackageRegistration
            {
                Key = 1,
                Id = packageId
            };

            var package = new Package { PackageStatusKey = status };
            var user = new User { Key = 2 };

            var certificate = new Certificate
            {
                Key = 3,
                Thumbprint = thumbprint ?? Guid.NewGuid().ToString()
            };

            user.UserCertificates.Add(new UserCertificate
            {
                Key = 4,
                CertificateKey = certificate.Key,
                Certificate = certificate,
                UserKey = user.Key,
                User = user
            });

            packageRegistration.Owners.Add(user);

            corePackageService
                .Setup(x => x.FindPackageRegistrationById(packageId))
                .Returns(packageRegistration);

            corePackageService
                .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, packageVersion))
                .Returns(package);
        }
    }
}