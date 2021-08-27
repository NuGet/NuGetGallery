// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class PackageRegistrationAuditRecord : AuditRecord<AuditedPackageRegistrationAction>
    {
        public string Id { get; }
        public AuditedPackageRegistration RegistrationRecord { get; }
        public string Owner { get; }
        public string PreviousRequiredSigner { get; private set; }
        public string NewRequiredSigner { get; private set; }
        public string RequestingOwner { get; private set; }
        public string NewOwner { get; private set; }

        public PackageRegistrationAuditRecord(
            string id, AuditedPackageRegistration registrationRecord, AuditedPackageRegistrationAction action, string owner)
            : base(action)
        {
            Id = id;
            RegistrationRecord = registrationRecord;
            Owner = owner;
        }

        public PackageRegistrationAuditRecord(
            PackageRegistration packageRegistration, AuditedPackageRegistrationAction action, string owner)
            : this(packageRegistration.Id, AuditedPackageRegistration.CreateFrom(packageRegistration), action, owner)
        {
        }

        public override string GetPath()
        {
            return $"{Id}".ToLowerInvariant();
        }

        public static PackageRegistrationAuditRecord CreateForSetRequiredSigner(
            PackageRegistration registration,
            string previousRequiredSigner,
            string newRequiredSigner)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            var record = new PackageRegistrationAuditRecord(
                registration,
                AuditedPackageRegistrationAction.SetRequiredSigner,
                owner: null);

            record.PreviousRequiredSigner = previousRequiredSigner;
            record.NewRequiredSigner = newRequiredSigner;

            return record;
        }

        private static PackageRegistrationAuditRecord CreateForOwnerRequest(
            PackageRegistration registration,
            string requestingOwner,
            string newOwner,
            AuditedPackageRegistrationAction action)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var record = new PackageRegistrationAuditRecord(registration, action, owner: null);

            record.RequestingOwner = requestingOwner;
            record.NewOwner = newOwner;

            return record;
        }

        public static PackageRegistrationAuditRecord CreateForAddOwnershipRequest(
            PackageRegistration registration,
            string requestingOwner,
            string newOwner)
        {
            return CreateForOwnerRequest(
                registration,
                requestingOwner,
                newOwner,
                AuditedPackageRegistrationAction.AddOwnershipRequest);
        }

        public static PackageRegistrationAuditRecord CreateForDeleteOwnershipRequest(
            PackageRegistration registration,
            string requestingOwner,
            string newOwner)
        {
            return CreateForOwnerRequest(
                registration,
                requestingOwner,
                newOwner,
                AuditedPackageRegistrationAction.DeleteOwnershipRequest);
        }
    }
}