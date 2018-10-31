// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditedPackageFacts
    {

        [Fact]
        public void CreateFromPackage_PreservesOriginalAndNormalizedVersionStrings()
        {
            var package = new Package
            {
                Hash = "a",
                PackageRegistration = new PackageRegistration() {Id = "B"},
                Version = "1.0.0+c",
                NormalizedVersion = "1.0.0"
            };

            var auditedPackage = AuditedPackage.CreateFrom(package);

            Assert.Equal(package.Version, auditedPackage.Version);
            Assert.Equal(package.NormalizedVersion, auditedPackage.NormalizedVersion);
        }
    }
}