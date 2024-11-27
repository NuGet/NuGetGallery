// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Auditing
{
    public enum AuditedFederatedCredentialAction
    {
        /// <summary>
        /// A federated credential was created.
        /// </summary>
        Create,
    }

    public class FederatedCredentialAuditRecord : AuditRecord<AuditedFederatedCredentialAction>
    {
        public int Key { get; }
        public FederatedCredentialType Type { get; }
        public string Identity { get; }

        public FederatedCredentialAuditRecord(
            AuditedFederatedCredentialAction action,
            FederatedCredential federatedCredential) : base(action)
        {
            Key = federatedCredential.Key;
            Type = federatedCredential.Type;
            Identity = federatedCredential.Identity;
        }

        public override string GetPath()
        {
            return $"{Type}/{Identity}/{Key}";
        }
    }
}
