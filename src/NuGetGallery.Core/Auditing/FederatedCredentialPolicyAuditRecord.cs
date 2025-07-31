// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;

#nullable enable

namespace NuGetGallery.Auditing
{
    public enum AuditedFederatedCredentialPolicyAction
    {
        /// <summary>
        /// A federated credential policy was created.
        /// </summary>
        Create,

        /// <summary>
        /// A federated credential policy was deleted.
        /// </summary>
        Delete,

        /// <summary>
        /// An external security token was compared to a federated credential policy. The comparison is either successful
        /// (policy criteria match) or unsuccessful (no match).
        /// </summary>
        Compare,

        /// <summary>
        /// A federated credential policy matched a federated credential.
        /// </summary>
        ExchangeForApiKey,

        /// <summary>
        /// A federated credential matched a policy but rejected due to token replace protection.
        /// </summary>
        RejectReplay,

        /// <summary>
        /// Indicates that the federated credential policy was updated on first use, after all
        /// required criteria were satisfied. For example, a GitHub Actions policy may be
        /// updated with the repository owner and repository ID upon its first use.
        /// </summary>
        FirstUsePolicyUpdate,

        /// <summary>
        /// A federated credential policy was modified.
        /// </summary>
        Update,

        /// <summary>
        /// Bad request occurred while creating a <see cref="FederatedCredentialPolicy"/>.
        /// </summary>
        BadRequest,

        /// <summary>
        /// Unauthorized request while creating a <see cref="FederatedCredentialPolicy"/>.
        /// </summary>
        Unauthorized,
    }

    public class FederatedCredentialPolicyAuditRecord : AuditRecord<AuditedFederatedCredentialPolicyAction>
    {
        public const string ResourceType = "FederatedCredentialPolicy";

        public override string GetResourceType() => ResourceType;

        private FederatedCredentialPolicyAuditRecord(
            AuditedFederatedCredentialPolicyAction action,
            FederatedCredentialPolicy policy,
            bool success,
            FederatedCredential? federatedCredential,
            IReadOnlyList<CredentialAuditRecord> apiKeyCredentials,
            ExternalSecurityTokenAuditRecord? externalCredential) : this(
                action,
                policy.Key,
                policy.Type,
                policy.CreatedBy,
                policy.PackageOwner,
                policy.Criteria,
                success,
                federatedCredential,
                apiKeyCredentials,
                externalCredential,
                null)
        {
        }

        private FederatedCredentialPolicyAuditRecord(
            AuditedFederatedCredentialPolicyAction action,
            int? key,
            FederatedCredentialType type,
            User createdBy,
            User packageOwner,
            string criteria,
            bool success,
            FederatedCredential? federatedCredential,
            IReadOnlyList<CredentialAuditRecord> apiKeyCredentials,
            ExternalSecurityTokenAuditRecord? externalCredential,
            string? errorMessage) : base(action)
        {
            Key = key;
            Type = type.ToString();
            Criteria = criteria;
            CreatedByUsername = createdBy.Username;
            PackageOwnerUsername = packageOwner.Username;

            Success = success;
            FederatedCredential = federatedCredential is null ? null : new FederatedCredentialAuditRecord(AuditedFederatedCredentialAction.Create, federatedCredential);
            ApiKeyCredentials = apiKeyCredentials;
            ExternalCredential = externalCredential;
            ErrorMessage = errorMessage;
        }

        public int? Key { get; }
        public string Type { get; }
        public string Criteria { get; }
        public string CreatedByUsername { get; }
        public string PackageOwnerUsername { get; }
        public FederatedCredentialAuditRecord? FederatedCredential { get; }
        public IReadOnlyList<CredentialAuditRecord> ApiKeyCredentials { get; }
        public bool Success { get; }
        public ExternalSecurityTokenAuditRecord? ExternalCredential { get; }
        public string? ErrorMessage { get; }

        public static FederatedCredentialPolicyAuditRecord Compare(
            FederatedCredentialPolicy policy,
            ExternalSecurityTokenAuditRecord externalCredential,
            bool success)
        {
            if (externalCredential.IssuerType == FederatedCredentialIssuerType.Unsupported
                || externalCredential.Action != AuditedExternalSecurityTokenAction.Validated)
            {
                throw new ArgumentException("Only validated external credentials can be compared with a policy.");
            }

            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.Compare,
                policy,
                success,
                federatedCredential: null,
                apiKeyCredentials: [],
                externalCredential: externalCredential);
        }

        public static FederatedCredentialPolicyAuditRecord ExchangeForApiKey(
            FederatedCredentialPolicy policy,
            FederatedCredential federatedCredential,
            Credential createdApiKeyCredential)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.ExchangeForApiKey,
                policy,
                success: true,
                federatedCredential,
                apiKeyCredentials: [new CredentialAuditRecord(createdApiKeyCredential)],
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord RejectReplay(
            FederatedCredentialPolicy policy,
            FederatedCredential federatedCredential)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.RejectReplay,
                policy,
                success: false,
                federatedCredential,
                apiKeyCredentials: [],
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord Delete(
            FederatedCredentialPolicy policy,
            IReadOnlyList<Credential> deletedApiKeyCredentials)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.Delete,
                policy,
                success: true,
                federatedCredential: null,
                apiKeyCredentials: deletedApiKeyCredentials.Select(x => new CredentialAuditRecord(x)).ToList(),
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord Update(
            FederatedCredentialPolicy policy,
            IReadOnlyList<Credential> deletedApiKeyCredentials)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.Update,
                policy,
                success: true,
                federatedCredential: null,
                apiKeyCredentials: deletedApiKeyCredentials.Select(x => new CredentialAuditRecord(x)).ToList(),
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord Create(
            FederatedCredentialPolicy policy)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.Create,
                policy,
                success: true,
                federatedCredential: null,
                apiKeyCredentials: [],
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord FirstUseUpdate(
            FederatedCredentialPolicy policy)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.FirstUsePolicyUpdate,
                policy,
                success: true,
                federatedCredential: null,
                apiKeyCredentials: [],
                externalCredential: null);
        }

        public static FederatedCredentialPolicyAuditRecord BadRequest(
            FederatedCredentialPolicy policy, string errorMessage)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.BadRequest,
                policy.Key,
                policy.Type,
                policy.CreatedBy,
                policy.PackageOwner,
                policy.Criteria,
                success: false,
                federatedCredential: null,
                apiKeyCredentials: [],
                externalCredential: null,
                errorMessage: errorMessage ?? throw new ArgumentNullException(nameof(errorMessage)));
        }

        public static FederatedCredentialPolicyAuditRecord Unauthorized(
            FederatedCredentialPolicy policy, string errorMessage)
        {
            return new FederatedCredentialPolicyAuditRecord(
                AuditedFederatedCredentialPolicyAction.Unauthorized,
                policy.Key,
                policy.Type,
                policy.CreatedBy,
                policy.PackageOwner,
                policy.Criteria,
                success: false,
                federatedCredential: null,
                apiKeyCredentials: [],
                externalCredential: null,
                errorMessage: errorMessage ?? throw new ArgumentNullException(nameof(errorMessage)));
        }

        public override string GetPath()
        {
            return $"{Type}/{Key?.ToString() ?? "no-key"}";
        }
    }
}
