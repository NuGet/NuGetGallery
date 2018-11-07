// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace NuGet.Services.Validation
{
    public class PackageValidatingEntityFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageNormalizedVersion = "1.0.0";
        private static DateTime PackageCreated = new DateTime(2018, 4, 4, 4, 4, 4);

        [Fact]
        public void PropertiesValidation()
        {
            // Arrange
            var package = CreatePackage();

            // Act & Assert
            var validatingEntity = new PackageValidatingEntity(package);

            Assert.Equal(PackageCreated, validatingEntity.Created);
            Assert.Equal(PackageKey, validatingEntity.Key);
            Assert.Equal(PackageId, validatingEntity.EntityRecord.PackageRegistration.Id);
            Assert.Equal(PackageNormalizedVersion, validatingEntity.EntityRecord.NormalizedVersion);
            Assert.Equal(ValidatingType.Package, validatingEntity.ValidatingType);
        }

        private static Package CreatePackage()
        {
            return new Package()
            {
                NormalizedVersion = PackageNormalizedVersion,
                PackageRegistration = new PackageRegistration()
                {
                    Id = PackageId
                },
                Key = PackageKey,
                Created = PackageCreated
            };
        }
    }
}
