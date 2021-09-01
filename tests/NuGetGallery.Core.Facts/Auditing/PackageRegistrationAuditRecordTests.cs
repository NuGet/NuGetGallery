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
        [Fact]
        public void Constructor_SetsProperties()
        {
            var record = new PackageRegistrationAuditRecord(
                new PackageRegistration() { Id = "a" },
                AuditedPackageRegistrationAction.AddOwner,
                owner: "b");

            Assert.Equal("a", record.Id);
            Assert.NotNull(record.RegistrationRecord);
            Assert.Equal("a", record.RegistrationRecord.Id);
            Assert.Equal("b", record.Owner);
            Assert.Equal(AuditedPackageRegistrationAction.AddOwner, record.Action);
        }

        [Fact]
        public void GetPath_ReturnsLowerCasedId()
        {
            var record = new PackageRegistrationAuditRecord(
                new PackageRegistration() { Id = "A" },
                AuditedPackageRegistrationAction.AddOwner,
                owner: "b");

            var actualPath = record.GetPath();

            Assert.Equal("a", actualPath);
        }

        [Fact]
        public void CreateForAddOwnershipRequest_InitializesProperties()
        {
            var record = PackageRegistrationAuditRecord.CreateForAddOwnershipRequest(
                new PackageRegistration { Id = "NuGet.Versioning" },
                requestingOwner: "NuGet",
                newOwner: "Microsoft");

            Assert.Equal("NuGet.Versioning", record.Id);
            Assert.Equal("NuGet", record.RequestingOwner);
            Assert.Equal("Microsoft", record.NewOwner);
            Assert.Equal(AuditedPackageRegistrationAction.AddOwnershipRequest, record.Action);
        }

        [Fact]
        public void CreateForDeleteOwnershipRequest_InitializesProperties()
        {
            var record = PackageRegistrationAuditRecord.CreateForDeleteOwnershipRequest(
                new PackageRegistration { Id = "NuGet.Versioning" },
                requestingOwner: "NuGet",
                newOwner: "Microsoft");

            Assert.Equal("NuGet.Versioning", record.Id);
            Assert.Equal("NuGet", record.RequestingOwner);
            Assert.Equal("Microsoft", record.NewOwner);
            Assert.Equal(AuditedPackageRegistrationAction.DeleteOwnershipRequest, record.Action);
        }

        [Fact]
        public void CreateForSetRequiredSigner_WhenRegistrationIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => PackageRegistrationAuditRecord.CreateForSetRequiredSigner(
                    registration: null,
                    previousRequiredSigner: "a",
                    newRequiredSigner: "b"));

            Assert.Equal("registration", exception.ParamName);
        }

        [Theory]
        [MemberData(nameof(RequiredSignerTests))]
        public void CreateForSetRequiredSigner_InitializesProperties(RequiredSignerTest test)
        {
            var record = PackageRegistrationAuditRecord.CreateForSetRequiredSigner(
                test.PackageRegistration, test.PreviousRequiredSigner, test.NewRequiredSigner);

            Assert.Equal(AuditedPackageRegistrationAction.SetRequiredSigner, record.Action);
            Assert.Equal(test.PackageRegistration.Id, record.Id);
            Assert.Equal(test.PreviousRequiredSigner, record.PreviousRequiredSigner);
            Assert.Equal(test.NewRequiredSigner, record.NewRequiredSigner);
            Assert.Null(record.Owner);
            Assert.Equal(test.PackageRegistration.Id, record.RegistrationRecord.Id);
        }

        public static IEnumerable<object[]> RequiredSignerTests
        {
            get
            {
                var packageRegistration = new PackageRegistration()
                {
                    Id = "a"
                };

                yield return new[]
                {
                    new RequiredSignerTest(
                        packageRegistration,
                        previousRequiredSigner: null,
                        newRequiredSigner: "b")
                };

                yield return new[]
                {
                    new RequiredSignerTest(
                        packageRegistration,
                        previousRequiredSigner: "b",
                        newRequiredSigner: null)
                };

                yield return new[]
                {
                    new RequiredSignerTest(
                        packageRegistration,
                        previousRequiredSigner: "b",
                        newRequiredSigner: "c")
                };
            }
        }

        public sealed class RequiredSignerTest
        {
            internal PackageRegistration PackageRegistration { get; }
            internal string PreviousRequiredSigner { get; }
            internal string NewRequiredSigner { get; }

            internal RequiredSignerTest(
                PackageRegistration registration,
                string previousRequiredSigner,
                string newRequiredSigner)
            {
                PackageRegistration = registration;
                PreviousRequiredSigner = previousRequiredSigner;
                NewRequiredSigner = newRequiredSigner;
            }
        }
    }
}