// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services.Authentication;

#nullable enable

namespace NuGetGallery.Auditing
{
    public enum AuditedExternalSecurityTokenAction
    {
        /// <summary>
        /// The external security token was rejected, due to invalid format, issuer, claims, signature, or other reasons.
        /// </summary>
        Rejected,

        /// <summary>
        /// The external security token was validated against a known issuer. The token can be used to compare against
        /// a federated credential trust policy.
        /// </summary>
        Validated,
    }

    public class ExternalSecurityTokenAuditRecord : AuditRecord<AuditedExternalSecurityTokenAction>
    {
        public const string ResourceType = "ExternalSecurityToken";

        public override string GetResourceType() => ResourceType;

        /// <summary>
        /// The issuer type of the token. This is the <see cref="Issuer"/> value categorized.
        /// </summary>
        public FederatedCredentialIssuerType IssuerType { get; }

        /// <summary>
        /// A string representation of the issuer of the token. For JSON web tokens, this is the "iss" claim. If missing,
        /// this will be the value "MISSING_ISSUER".
        /// </summary>
        public string Issuer { get; }

        /// <summary>
        /// A string representation of the claims in the token. For JSON web tokens, this is the payload JSON.
        /// It is the responsibility of the auditing service to redact PII from this field.
        /// </summary>
        public string? Claims { get; }

        /// <summary>
        /// The unique identifier of the external security token. For JSON web tokens, this is the "jti" or "uti" claim.
        /// If missing, this will be the value "MISSING_IDENTITY".
        /// </summary>
        public string Identity { get; }

        public ExternalSecurityTokenAuditRecord(
            AuditedExternalSecurityTokenAction action,
            FederatedCredentialIssuerType issuerType,
            string? issuer,
            string? claims,
            string? identity) : base(action)
        {
            IssuerType = issuerType;
            Issuer = string.IsNullOrWhiteSpace(issuer) ? "MISSING_ISSUER" : issuer!;
            Claims = string.IsNullOrWhiteSpace(claims) ? null : claims;
            Identity = string.IsNullOrWhiteSpace(identity) ? "MISSING_IDENTITY" : identity!;
        }

        public override string GetPath()
        {
            return $"{IssuerType}/{Identity}";
        }
    }
}
