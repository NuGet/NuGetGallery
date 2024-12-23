// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialPolicyEvaluator
    {
        Task<EvaluatedFederatedCredentialPolicies> GetMatchingPolicyAsync(
            IReadOnlyCollection<FederatedCredentialPolicy> policies,
            string bearerToken,
            NameValueCollection requestHeaders);
    }

    public class FederatedCredentialPolicyEvaluator : IFederatedCredentialPolicyEvaluator
    {
        private readonly IEntraIdTokenValidator _entraIdTokenValidator;
        private readonly IReadOnlyList<IFederatedCredentialValidator> _additionalValidators;
        private readonly IAuditingService _auditingService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<FederatedCredentialPolicyEvaluator> _logger;

        public FederatedCredentialPolicyEvaluator(
            IEntraIdTokenValidator entraIdTokenValidator,
            IReadOnlyList<IFederatedCredentialValidator> additionalValidators,
            IAuditingService auditingService,
            IDateTimeProvider dateTimeProvider,
            ILogger<FederatedCredentialPolicyEvaluator> logger)
        {
            _entraIdTokenValidator = entraIdTokenValidator ?? throw new ArgumentNullException(nameof(entraIdTokenValidator));
            _additionalValidators = additionalValidators ?? throw new ArgumentNullException(nameof(additionalValidators));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EvaluatedFederatedCredentialPolicies> GetMatchingPolicyAsync(
            IReadOnlyCollection<FederatedCredentialPolicy> policies,
            string bearerToken,
            NameValueCollection requestHeaders)
        {
            // perform basic validations not specific to any federated credential policy
            // the error message is user-facing and should not leak sensitive information
            var (userError, jwtInfo) = await ValidateJwtByIssuer(bearerToken);

            // Whether or not we have detected a problem already, pass the information to all additional validators.
            // This allows custom logic to execute but will not override any initial failed validation result.
            userError = await ExecuteAdditionalValidatorsAsync(requestHeaders, userError, jwtInfo);

            var externalCredentialAudit = jwtInfo.CreateAuditRecord();
            await AuditExternalCredentialAsync(externalCredentialAudit);

            if (userError is not null)
            {
                _logger.LogInformation("The bearer token failed validation. Reason: {UserError}", userError);
                return EvaluatedFederatedCredentialPolicies.BadToken(userError);
            }

            if (!jwtInfo.IsValid || jwtInfo.IssuerType == FederatedCredentialIssuerType.Unsupported || jwtInfo.Jwt is null)
            {
                throw new InvalidOperationException("A recognized, valid JWT should be available.");
            }

            var results = new List<FederatedCredentialPolicyResult>();

            // sort the policy results by creation date, so older policy results are preferred
            foreach (var policy in policies.OrderBy(x => x.Created))
            {
                var result = EvaluatePolicy(policy, jwtInfo);

                results.Add(result);

                var success = result.Type == FederatedCredentialPolicyResultType.Success;
                await AuditPolicyComparisonAsync(externalCredentialAudit, policy, success);
                if (success)
                {
                    return EvaluatedFederatedCredentialPolicies.NewMatchedPolicy(results, result.Policy, result.FederatedCredential);
                }
            }

            return EvaluatedFederatedCredentialPolicies.NoMatchingPolicy(results);
        }

        private async Task<string?> ExecuteAdditionalValidatorsAsync(NameValueCollection requestHeaders, string? userError, JwtInfo jwtInfo)
        {
            bool hasUnauthorized = false;
            foreach (var validator in _additionalValidators)
            {
                var result = await validator.ValidateAsync(requestHeaders, jwtInfo.IssuerType, jwtInfo.Jwt?.Claims);
                switch (result.Type)
                {
                    case FederatedCredentialValidationType.Unauthorized:
                        // prefer the first user error, which will be the built-in user error if the bearer token is already rejected
                        userError ??= result.UserError;
                        hasUnauthorized = true;
                        if (jwtInfo.IsValid)
                        {
                            _logger.LogWarning(
                                "With issuer type {IssuerType}, the additional validator {Type} rejected the request, but the base validation accepted the bearer token. " +
                                "Additional validator user message: {UserError}",
                                jwtInfo.IssuerType,
                                validator.GetType().FullName,
                                result.UserError ?? "(none)");
                        }
                        break;
                    case FederatedCredentialValidationType.Valid:
                        if (!jwtInfo.IsValid)
                        {
                            _logger.LogWarning(
                                "With issuer type {IssuerType}, the additional validator {Type} accepted the request, but the base validation rejected the bearer token.",
                                jwtInfo.IssuerType,
                                validator.GetType().FullName);
                        }
                        break;
                    case FederatedCredentialValidationType.NotApplicable:
                        break;
                    default:
                        throw new NotImplementedException("Unsupported validation type:" + result.Type);
                }
            }

            if (hasUnauthorized)
            {
                userError ??= "The request could not be authenticated.";
                jwtInfo.IsValid = false;
            }

            return userError;
        }

        private async Task AuditPolicyComparisonAsync(ExternalSecurityTokenAuditRecord externalCredentialAudit, FederatedCredentialPolicy policy, bool success)
        {
            await _auditingService.SaveAuditRecordAsync(FederatedCredentialPolicyAuditRecord.Compare(
                policy,
                externalCredentialAudit,
                success));
        }

        private async Task AuditExternalCredentialAsync(ExternalSecurityTokenAuditRecord externalCredentialAudit)
        {
            await _auditingService.SaveAuditRecordAsync(externalCredentialAudit);
        }

        private FederatedCredentialPolicyResult EvaluatePolicy(FederatedCredentialPolicy policy, JwtInfo info)
        {
            // Evaluate the policy and return an unauthorized result if the policy does not match.
            // The reason is not shared with the caller to prevent leaking sensitive information.
            string? unauthorizedReason = (info.IssuerType, policy.Type) switch
            {
                (FederatedCredentialIssuerType.EntraId, FederatedCredentialType.EntraIdServicePrincipal) => EvaluateEntraIdServicePrincipal(policy, info.Jwt!),
                _ => "The policy type does not match the token issuer.",
            };

            FederatedCredentialPolicyResult result;
            if (unauthorizedReason is not null)
            {
                result = FederatedCredentialPolicyResult.Unauthorized(policy, unauthorizedReason);
            }
            else
            {
                result = FederatedCredentialPolicyResult.Success(policy, new FederatedCredential
                {
                    Identity = info.Identifier,
                    FederatedCredentialPolicyKey = policy.Key,
                    Type = policy.Type,
                    Created = _dateTimeProvider.UtcNow,
                    Expires = info.Jwt!.ValidTo.ToUniversalTime(),
                });
            }

            _logger.LogInformation(
                "Evaluated policy key {PolicyKey} of type {PolicyType}. Result type: {ResultType}. Reason: {Reason}",
                result.Policy.Key,
                result.Policy.Type,
                result.Type,
                unauthorizedReason ?? "policy matched token");

            return result;
        }

        private class JwtInfo
        {
            public bool IsValid { get; set; }

            public JsonWebToken? Jwt { get; set; }

            /// <summary>
            /// This should be the unique token identifier (uti for Entra ID or jti otherwise).
            /// Used to prevent replay and persisted on the <see cref="FederatedCredential"/> entity.
            /// </summary>
            public string? Identifier { get; set; }

            public FederatedCredentialIssuerType IssuerType { get; set; } = FederatedCredentialIssuerType.Unsupported;

            public ExternalSecurityTokenAuditRecord CreateAuditRecord()
            {
                var payload = Jwt?.EncodedPayload;
                string? claims = null;
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    claims = Base64UrlEncoder.Decode(payload);
                }

                return new ExternalSecurityTokenAuditRecord(
                    IsValid ? AuditedExternalSecurityTokenAction.Validated : AuditedExternalSecurityTokenAction.Rejected,
                    IssuerType,
                    Jwt?.Issuer,
                    claims,
                    Identifier);
            }
        }

        /// <summary>
        /// Parse the bearer token as a JWT, perform basic validation, and find the applicable that apply to all issuers.
        /// </summary>
        private async Task<(string? UserError, JwtInfo Info)> ValidateJwtByIssuer(string bearerToken)
        {
            var jwtInfo = new JwtInfo();

            try
            {
                jwtInfo.Jwt = new JsonWebToken(bearerToken);
            }
            catch (ArgumentException)
            {
                return ("The bearer token could not be parsed as a JSON web token.", jwtInfo);
            }

            if (jwtInfo.Jwt.Audiences.Count() != 1)
            {
                return ("The JSON web token must have exactly one aud claim value.", jwtInfo);
            }

            if (string.IsNullOrWhiteSpace(jwtInfo.Jwt.Audiences.Single()))
            {
                return ("The JSON web token must have an aud claim.", jwtInfo);
            }

            if (string.IsNullOrWhiteSpace(jwtInfo.Jwt.Issuer))
            {
                return ("The JSON web token must have an iss claim.", jwtInfo);      
            }

            if (!Uri.TryCreate(jwtInfo.Jwt.Issuer, UriKind.Absolute, out var issuerUrl)
                || issuerUrl.Scheme != "https")
            {
                return ("The JSON web token iss claim must be a valid HTTPS URL.", jwtInfo);
            }

            FederatedCredentialIssuerType issuerType;
            string? userError;
            string? identifier;
            TokenValidationResult? validationResult;
            switch (issuerUrl.Authority)
            {
                case "login.microsoftonline.com":
                    issuerType = FederatedCredentialIssuerType.EntraId;
                    (userError, identifier, validationResult) = await GetEntraIdValidationResultAsync(jwtInfo.Jwt);
                    break;
                default:
                    return ("The JSON web token iss claim is not supported.", jwtInfo);
            }

            jwtInfo.IssuerType = issuerType;
            jwtInfo.Identifier = identifier;

            if (userError is not null)
            {
                return (userError, jwtInfo);
            }

            if (string.IsNullOrWhiteSpace(identifier) || validationResult is null)
            {
                throw new InvalidOperationException("The identifier and validation result must be set.");
            }

            if (validationResult.IsValid)
            {
                jwtInfo.IsValid = true;
                return (null, jwtInfo);
            }

            var validationException = validationResult.Exception;

            userError = validationException switch
            {
                SecurityTokenExpiredException => "The JSON web token has expired.",
                SecurityTokenInvalidAudienceException => "The JSON web token has an incorrect audience.",
                SecurityTokenInvalidSignatureException => "The JSON web token has an invalid signature.",
                _ => "The JSON web token could not be validated.",
            };

            _logger.LogInformation(validationException, "The JSON web token with recognized issuer {Issuer} could not be validated.", jwtInfo.IssuerType);

            return (userError, jwtInfo);
        }
        
        private async Task<(string? UserError, string? Identifier, TokenValidationResult? Result)> GetEntraIdValidationResultAsync(JsonWebToken jwt)
        {
            const string UniqueTokenIdentifierClaim = "uti"; // unique token identifier (equivalent to jti)

            if (!jwt.TryGetPayloadValue<string>(UniqueTokenIdentifierClaim, out var uti)
                || string.IsNullOrWhiteSpace(uti))
            {
                return ($"The JSON web token must have a {UniqueTokenIdentifierClaim} claim.", null, null);
            }

            var tokenValidationResult = await _entraIdTokenValidator.ValidateAsync(jwt);
            return (null, uti, tokenValidationResult);
        }

        private string? EvaluateEntraIdServicePrincipal(FederatedCredentialPolicy policy, JsonWebToken jwt)
        {
            // See https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference
            const string ClientCredentialTypeClaim = "azpacr";
            const string ClientCertificateType = "2"; // 2 indicates a client certificate (or managed identity) was used
            const string IdentityTypeClaim = "idtyp";
            const string AppIdentityType = "app";
            const string VersionClaim = "ver";
            const string Version2 = "2.0";

            if (!jwt.TryGetPayloadValue<string>(ClaimConstants.Tid, out var tid))
            {
                return $"The JSON web token is missing the {ClaimConstants.Tid} claim.";
            }

            if (!jwt.TryGetPayloadValue<string>(ClaimConstants.Oid, out var oid))
            {
                return $"The JSON web token is missing the {ClaimConstants.Oid} claim.";
            }

            if (!jwt.TryGetPayloadValue<string>(ClientCredentialTypeClaim, out var azpacr))
            {
                return $"The JSON web token is missing the {ClientCredentialTypeClaim} claim.";
            }

            if (azpacr != ClientCertificateType)
            {
                return $"The JSON web token must have an {ClientCredentialTypeClaim} claim with a value of {ClientCertificateType}.";
            }

            if (!jwt.TryGetPayloadValue<string>(IdentityTypeClaim, out var idtyp))
            {
                return $"The JSON web token is missing the {IdentityTypeClaim} claim.";
            }

            if (idtyp != AppIdentityType)
            {
                return $"The JSON web token must have an {IdentityTypeClaim} claim with a value of {AppIdentityType}.";
            }

            if (!jwt.TryGetPayloadValue<string>(VersionClaim, out var ver))
            {
                return $"The JSON web token is missing the {VersionClaim} claim.";
            }

            if (ver != Version2)
            {
                return $"The JSON web token must have a {VersionClaim} claim with a value of {Version2}.";
            }

            if (jwt.Subject != oid)
            {
                return $"The JSON web token {ClaimConstants.Sub} claim must match the {ClaimConstants.Oid} claim.";
            }

            var criteria = DeserializePolicy<EntraIdServicePrincipalCriteria>(policy);

            if (string.IsNullOrWhiteSpace(tid) || !Guid.TryParse(tid, out var parsedTid) || parsedTid != criteria.TenantId)
            {
                return $"The JSON web token must have a {ClaimConstants.Tid} claim that matches the policy.";
            }

            if (!_entraIdTokenValidator.IsTenantAllowed(parsedTid))
            {
                return "The tenant ID in the JSON web token is not in allow list.";
            }

            if (string.IsNullOrWhiteSpace(oid) || !Guid.TryParse(oid, out var parsedOid) || parsedOid != criteria.ObjectId)
            {
                return $"The JSON web token must have a {ClaimConstants.Oid} claim that matches the policy.";
            }

            return null;
        }

        private static T DeserializePolicy<T>(FederatedCredentialPolicy policy)
        {
            var criteria = JsonSerializer.Deserialize<T>(policy.Criteria);
            if (criteria is null)
            {
                throw new InvalidOperationException("The policy criteria must be a valid JSON object.");
            }

            return criteria;
        }
    }
}
