// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;

namespace NuGetGallery.Auditing
{
    public class FailedAuthenticatedOperationAuditRecord 
        : AuditRecord<AuditedAuthenticatedOperationAction>
    {
        private const string Path = "all";

        public string UsernameOrEmail { get; }
        public AuditedPackageIdentifier AttemptedPackage { get; }
        public CredentialAuditRecord AttemptedCredential { get; }

        public FailedAuthenticatedOperationAuditRecord(
            string usernameOrEmail, 
            AuditedAuthenticatedOperationAction action,
            AuditedPackageIdentifier attemptedPackage = null,
            Credential attemptedCredential = null) 
            : base(action)
        {
            UsernameOrEmail = usernameOrEmail;

            if (attemptedPackage != null)
            {
                AttemptedPackage = attemptedPackage;
            }

            if (attemptedCredential != null)
            {
                AttemptedCredential = new CredentialAuditRecord(attemptedCredential, removedOrRevoked: false);
            }
        }

        public override string GetPath()
        {
            return Path; // store in <auditpath>/failedauthenticatedoperation/all
        }
    }
}