// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace NuGet.Services.Validation
{
    public class SymbolPackageValidatingEntityFacts
    {
        private const int PackageKey = 1001;
        private const int SymbolPackageKey = 1002;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageNormalizedVersion = "1.0.0";
        private static DateTime PackageCreated = new DateTime(2018, 4, 4, 4, 4, 4);
        private static DateTime SymbolPackageCreated = new DateTime(2018, 5, 4, 4, 4, 4);

        [Fact]
        public void PropertiesValidation()
        {
            // Arrange
            var symbolPackage = CreateSymbolPackage();

            // Act & Assert
            var validatingEntity = new SymbolPackageValidatingEntity(symbolPackage);

            Assert.Equal(SymbolPackageCreated, validatingEntity.Created);
            Assert.Equal(SymbolPackageKey, validatingEntity.Key);
            Assert.Equal(ValidatingType.SymbolPackage, validatingEntity.ValidatingType);
            Assert.Equal(PackageStatus.Available, validatingEntity.Status);
        }

        private static SymbolPackage CreateSymbolPackage()
        {
            var package =  new Package()
            {
                NormalizedVersion = PackageNormalizedVersion,
                PackageRegistration = new PackageRegistration()
                {
                    Id = PackageId
                },
                Key = PackageKey,
                Created = PackageCreated
            };

            return new SymbolPackage()
            {
                Created = SymbolPackageCreated,
                Key = SymbolPackageKey,
                StatusKey = PackageStatus.Available
            };

        }
    }
}
