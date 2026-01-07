// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class PackageRegistrationAuditRecordTests
    {
        public class TheConstructor
        {
            [Fact]
            public void SetsProperties()
            {
                // Arrange
                var packageRegistration = new PackageRegistration() { Id = "TestPackage" };
                
                // Act
                var record = new PackageRegistrationAuditRecord(
                    packageRegistration,
                    AuditedPackageRegistrationAction.AddOwner,
                    owner: "TestOwner");

                // Assert
                Assert.Equal("TestPackage", record.Id);
                Assert.NotNull(record.RegistrationRecord);
                Assert.Equal("TestPackage", record.RegistrationRecord.Id);
                Assert.Equal("TestOwner", record.Owner);
                Assert.Equal(AuditedPackageRegistrationAction.AddOwner, record.Action);
            }
        }

        public class TheGetPathMethod
        {
            [Fact]
            public void ReturnsLowerCasedId()
            {
                // Arrange
                var record = new PackageRegistrationAuditRecord(
                    new PackageRegistration() { Id = "TestPackage" },
                    AuditedPackageRegistrationAction.AddOwner,
                    owner: "TestOwner");

                // Act
                var actualPath = record.GetPath();

                // Assert
                Assert.Equal("testpackage", actualPath);
            }
        }

        public class TheOwnershipRequestFactoryMethods
        {
            [Theory]
            [InlineData(nameof(PackageRegistrationAuditRecord.CreateForAddOwnershipRequest), AuditedPackageRegistrationAction.AddOwnershipRequest)]
            [InlineData(nameof(PackageRegistrationAuditRecord.CreateForDeleteOwnershipRequest), AuditedPackageRegistrationAction.DeleteOwnershipRequest)]
            public void InitializeProperties(string methodName, AuditedPackageRegistrationAction expectedAction)
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "NuGet.Versioning" };
                var requestingOwner = "NuGet";
                var newOwner = "Microsoft";

                // Act
                PackageRegistrationAuditRecord record;
                if (methodName == nameof(PackageRegistrationAuditRecord.CreateForAddOwnershipRequest))
                {
                    record = PackageRegistrationAuditRecord.CreateForAddOwnershipRequest(
                        packageRegistration, requestingOwner, newOwner);
                }
                else
                {
                    record = PackageRegistrationAuditRecord.CreateForDeleteOwnershipRequest(
                        packageRegistration, requestingOwner, newOwner);
                }

                // Assert
                Assert.Equal("NuGet.Versioning", record.Id);
                Assert.Equal("NuGet", record.RequestingOwner);
                Assert.Equal("Microsoft", record.NewOwner);
                Assert.Equal(expectedAction, record.Action);
            }

            [Fact]
            public void CreateForAddOwnershipRequest_WhenRegistrationIsNull_Throws()
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationAuditRecord.CreateForAddOwnershipRequest(
                        registration: null,
                        requestingOwner: "NuGet",
                        newOwner: "Microsoft"));

                Assert.Equal("registration", exception.ParamName);
            }

            [Fact]
            public void CreateForDeleteOwnershipRequest_WhenRegistrationIsNull_Throws()
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationAuditRecord.CreateForDeleteOwnershipRequest(
                        registration: null,
                        requestingOwner: "NuGet",
                        newOwner: "Microsoft"));

                Assert.Equal("registration", exception.ParamName);
            }
        }

        public class TheCreateForSetRequiredSignerMethod
        {
            [Fact]
            public void WhenRegistrationIsNull_Throws()
            {
                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(
                    () => PackageRegistrationAuditRecord.CreateForSetRequiredSigner(
                        registration: null,
                        previousRequiredSigner: "PreviousSigner",
                        newRequiredSigner: "NewSigner"));

                Assert.Equal("registration", exception.ParamName);
            }

            [Theory]
            [InlineData(null, "NewSigner")]
            [InlineData("PreviousSigner", null)]
            [InlineData("PreviousSigner", "NewSigner")]
            public void InitializesProperties(string previousRequiredSigner, string newRequiredSigner)
            {
                // Arrange
                var packageRegistration = new PackageRegistration() { Id = "TestPackage" };

                // Act
                var record = PackageRegistrationAuditRecord.CreateForSetRequiredSigner(
                    packageRegistration, previousRequiredSigner, newRequiredSigner);

                // Assert
                Assert.Equal(AuditedPackageRegistrationAction.SetRequiredSigner, record.Action);
                Assert.Equal("TestPackage", record.Id);
                Assert.Equal(previousRequiredSigner, record.PreviousRequiredSigner);
                Assert.Equal(newRequiredSigner, record.NewRequiredSigner);
                Assert.Null(record.Owner);
                Assert.Equal("TestPackage", record.RegistrationRecord.Id);
            }
        }
    }
}