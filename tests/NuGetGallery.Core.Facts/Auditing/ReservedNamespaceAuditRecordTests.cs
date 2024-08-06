// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class ReservedNamespaceAuditRecordTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            // Arrange
            var prefix = new ReservedNamespace("microsoft.", isSharedNamespace: false, isPrefix: true);
            var registrationsList = new List<PackageRegistration>
            {
                new PackageRegistration { Id = "Microsoft.Package1" },
                new PackageRegistration { Id = "Microsoft.AspNet.Package2" },
                new PackageRegistration { Id = "Microsoft.Package2" }
            };

            var owner = new User("microsoft");

            // Act
            var record = new ReservedNamespaceAuditRecord(prefix,
                AuditedReservedNamespaceAction.AddOwner,
                owner.Username,
                registrations: registrationsList);

            // Assert
            Assert.Equal(prefix.Value, record.Value);
            Assert.NotNull(record.AffectedReservedNamespace);
            Assert.NotNull(record.AffectedRegistrations);
            Assert.NotNull(record.AffectedOwner);
            Assert.Equal(prefix.Value, record.AffectedReservedNamespace.Value);
            Assert.Equal(prefix.IsSharedNamespace, record.AffectedReservedNamespace.IsSharedNamespace);
            Assert.Equal(prefix.IsPrefix, record.AffectedReservedNamespace.IsPrefix);
            Assert.Equal(AuditedReservedNamespaceAction.AddOwner, record.Action);
            Assert.Equal(registrationsList.Count, record.AffectedRegistrations.Length);
            Assert.Equal(owner.Username, record.AffectedOwner);
        }

        [Theory]
        [MemberData(nameof(InvalidReservedNamespaceActionsForPackageRegistrationAudit))]
        public void InvalidActionsThrowException(AuditedReservedNamespaceAction action)
        {
            var prefix = new ReservedNamespace("microsoft.", isSharedNamespace: false, isPrefix: true);
            var registrationsList = new List<PackageRegistration>
            {
                new PackageRegistration { Id = "Microsoft.Package1" },
                new PackageRegistration { Id = "Microsoft.AspNet.Package2" },
                new PackageRegistration { Id = "Microsoft.Package2" }
            };

            var owner = new User("microsoft");

            // Act
            Assert.Throws<ArgumentException>(() => new ReservedNamespaceAuditRecord(prefix, action, owner.Username, registrations: registrationsList));
        }

        [Fact]
        public void GetPath_ReturnsLowerCasedId()
        {
            var record = new ReservedNamespaceAuditRecord(
                new ReservedNamespace() { Value = "MicroSoft." },
                AuditedReservedNamespaceAction.ReserveNamespace);

            var actualPath = record.GetPath();

            Assert.Equal("microsoft.", actualPath);
        }

        public static IEnumerable<object[]> InvalidReservedNamespaceActionsForPackageRegistrationAudit
        {
            get
            {
                var allowedActions = new AuditedReservedNamespaceAction[] { AuditedReservedNamespaceAction.AddOwner, AuditedReservedNamespaceAction.RemoveOwner };
                var allActions = new List<AuditedReservedNamespaceAction>(Enum.GetValues(typeof(AuditedReservedNamespaceAction)) as AuditedReservedNamespaceAction[]);
                var invalidActions = allActions.Except(allowedActions);
                foreach (var action in invalidActions)
                {
                    yield return new object[] { action } ;
                }
            }
        }

    }
}