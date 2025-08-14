// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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
    /// <summary>
    /// Provides a unified interface for evaluating federated credential policies and OIDC tokens:
    /// <para> * <see cref="FederatedCredentialPolicy"/> validation during creation or update. </para>
    /// <para> * <see cref="JsonWebToken"/> OIDC token authentication. </para>
    /// <para> * Token-to-policy matching. </para>
    /// </summary>
    public interface IFederatedCredentialPolicyEvaluator
    {
        /// <summary>
        /// Validates a federated credential policy during its creation or update.
        /// </summary>
        FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy);

        /// <summary>
        /// Evaluates federated credential policies against a specified bearer token and request headers.
        /// </summary>
        Task<OidcTokenEvaluationResult> GetMatchingPolicyAsync(
            IReadOnlyCollection<FederatedCredentialPolicy> policies,
            string bearerToken,
            NameValueCollection requestHeaders);
    }

    public class FederatedCredentialPolicyEvaluator : IFederatedCredentialPolicyEvaluator
    {
        private readonly IReadOnlyList<ITokenPolicyValidator> _validators;
        private readonly IReadOnlyList<IFederatedCredentialValidator> _additionalValidators;
        private readonly IAuditingService _auditingService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<FederatedCredentialPolicyEvaluator> _logger;

        public FederatedCredentialPolicyEvaluator(
            IReadOnlyList<ITokenPolicyValidator> validators,
            IReadOnlyList<IFederatedCredentialValidator> additionalValidators,
            IAuditingService auditingService,
            IDateTimeProvider dateTimeProvider,
            ILogger<FederatedCredentialPolicyEvaluator> logger)
        {
            _validators = validators ?? throw new ArgumentNullException(nameof(validators));
            _additionalValidators = additionalValidators ?? throw new ArgumentNullException(nameof(additionalValidators));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy)
        {
            FederatedCredentialIssuerType issuerType = policy.Type switch
            {
                FederatedCredentialType.EntraIdServicePrincipal => FederatedCredentialIssuerType.EntraId,
                FederatedCredentialType.GitHubActions => FederatedCredentialIssuerType.GitHubActions,
                _ => throw new ArgumentException($"Unsupported {policy.Type}"),
            };

            var validator = _validators.FirstOrDefault(v => v.IssuerType == issuerType);
            if (validator == null)
            {
                throw new ArgumentException($"No validator found for issuer type {issuerType}.");
            }

            return validator.ValidatePolicy(policy);
        }

        public async Task<OidcTokenEvaluationResult> GetMatchingPolicyAsync(
            IReadOnlyCollection<FederatedCredentialPolicy> policies,
            string bearerToken,
            NameValueCollection requestHeaders)
        {
            // perform basic validations not specific to any federated credential policy
            // the error message is user-facing and should not leak sensitive information
            var context = await ValidateJwtByIssuerAsync(bearerToken);

            // Whether or not we have detected a problem already, pass the information to all additional validators.
            // This allows custom logic to execute but will not override any initial failed validation result.
            await ExecuteAdditionalValidatorsAsync(requestHeaders, context);

            var externalCredentialAudit = context.CreateAuditRecord();
            await AuditExternalCredentialAsync(externalCredentialAudit);

            if (context.Error is not null)
            {
                _logger.LogInformation($"The bearer token failed validation. Reason: {context.Error}");
                return OidcTokenEvaluationResult.BadToken(context.Error);
            }

            if (!context.IsValid || context.Jwt is null)
            {
                throw new InvalidOperationException("A recognized, valid JWT should be available.");
            }

            // sort the policy results by creation date, so older policy results are preferred
            string? disclosableError = null; // we'll use first disclosable error (if any)
            foreach (var policy in policies.OrderBy(x => x.Created))
            {
                var result = await EvaluatePolicyAsync(policy, context);

                var success = result.Type == FederatedCredentialPolicyResultType.Success;
                await AuditPolicyComparisonAsync(externalCredentialAudit, policy, success);
                if (success)
                {
                    var credential = new FederatedCredential
                    {
                        Identity = context.Identifier,
                        FederatedCredentialPolicyKey = policy.Key,
                        Type = policy.Type,
                        Created = _dateTimeProvider.UtcNow,
                        Expires = context.Jwt!.ValidTo.ToUniversalTime(),
                    };

                    return OidcTokenEvaluationResult.NewMatchedPolicy(policy, credential);
                }
                else if (disclosableError == null && result.IsErrorDisclosable && result.Error != null)
                {
                    disclosableError = result.Error;
                }
            }

            return OidcTokenEvaluationResult.NoMatchingPolicy(disclosableError);
        }

        private async Task ExecuteAdditionalValidatorsAsync(NameValueCollection requestHeaders, TokenContext context)
        {
            bool hasUnauthorized = false;
            foreach (var validator in _additionalValidators)
            {
                var result = await validator.ValidateAsync(requestHeaders, context.IssuerType, context.Jwt?.Claims);
                switch (result.Type)
                {
                    case FederatedCredentialValidationType.Unauthorized:
                        // prefer the first user error, which will be the built-in user error if the bearer token is already rejected
                        context.Error ??= result.UserError;
                        hasUnauthorized = true;
                        if (context.IsValid)
                        {
                            _logger.LogWarning(
                                "With issuer type {IssuerType}, the additional validator {Type} rejected the request, but the base validation accepted the bearer token. " +
                                "Additional validator user message: {UserError}",
                                context.IssuerType,
                                validator.GetType().FullName,
                                result.UserError ?? "(none)");
                        }
                        break;
                    case FederatedCredentialValidationType.Valid:
                        if (!context.IsValid)
                        {
                            _logger.LogWarning(
                                "With issuer type {IssuerType}, the additional validator {Type} accepted the request, but the base validation rejected the bearer token.",
                                context.IssuerType,
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
                context.Error ??= "The request could not be authenticated.";
            }
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

        private async Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, TokenContext context)
        {
            // We do not want to fail the entire evaluation if a single policy fails
            FederatedCredentialPolicyResult result;
            try
            {
                result = await context.TokenValidator!.EvaluatePolicyAsync(policy, context.Jwt!);
            }
            catch (Exception ex)
            {
                result = FederatedCredentialPolicyResult.Unauthorized($"{ex.GetType().FullName}: {ex.Message}");
            }

            if (result.Type != FederatedCredentialPolicyResultType.NotApplicable)
            {
                _logger.LogInformation(
                    "Evaluated policy key {PolicyKey} of type {PolicyType}. Result type: {ResultType}. Reason: {Reason}. Disclosable {Disclosable}",
                    policy.Key,
                    policy.Type,
                    result.Type,
                    result.Error,
                    result.IsErrorDisclosable);
            }

            return result;
        }

        /// <summary>
        /// Context for processing JWT.
        /// </summary>
        [DebuggerDisplay("error: {Error}")]
        private class TokenContext
        {
            public bool IsValid => Error == null && TokenValidator != null;

            public JsonWebToken? Jwt { get; set; }

            /// <summary>
            /// This should be the unique token identifier (uti for Entra ID or jti otherwise).
            /// Used to prevent replay and persisted on the <see cref="FederatedCredential"/> entity.
            /// </summary>
            public string? Identifier { get; set; }

            public ITokenPolicyValidator? TokenValidator { get; set; }

            public string? Error { get; set; }

            public FederatedCredentialIssuerType IssuerType => TokenValidator?.IssuerType ?? FederatedCredentialIssuerType.Unsupported;

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
        private async Task<TokenContext> ValidateJwtByIssuerAsync(string bearerToken)
        {
            var context = new TokenContext();

            try
            {
                context.Jwt = new JsonWebToken(bearerToken);
            }
            catch (ArgumentException)
            {
                context.Error = "The bearer token could not be parsed as a JSON web token.";
                return context;
            }

            if (context.Jwt.Audiences.Count() != 1)
            {
                context.Error = "The JSON web token must have exactly one aud claim value.";
                return context;
            }

            if (string.IsNullOrWhiteSpace(context.Jwt.Audiences.Single()))
            {
                context.Error = "The JSON web token must have an aud claim.";
                return context;
            }

            if (string.IsNullOrWhiteSpace(context.Jwt.Issuer))
            {
                context.Error = "The JSON web token must have an iss claim.";
                return context;
            }

            if (!Uri.TryCreate(context.Jwt.Issuer, UriKind.Absolute, out var issuerUrl)
                || issuerUrl.Scheme != "https")
            {
                context.Error = "The JSON web token iss claim must be a valid HTTPS URL.";
                return context;
            }

            context.TokenValidator = _validators.FirstOrDefault(v => string.Equals(v.IssuerAuthority, issuerUrl.Authority, StringComparison.OrdinalIgnoreCase));
            if (context.TokenValidator == null)
            {
                context.Error = "The JSON web token iss claim is not supported.";
                return context;
            }

            (string? tokenId, string? error) = context.TokenValidator.ValidateTokenIdentifier(context.Jwt);
            context.Error = error;
            context.Identifier = tokenId;
            if (!context.IsValid)
            {
                return context;
            }

            TokenValidationResult validationResult = await context.TokenValidator.ValidateTokenAsync(context.Jwt);
            if (string.IsNullOrWhiteSpace(tokenId) || validationResult is null)
            {
                throw new InvalidOperationException("The identifier and validation result must be set.");
            }

            if (!validationResult.IsValid)
            {
                var validationException = validationResult.Exception;

                context.Error = validationException switch
                {
                    SecurityTokenExpiredException => "The JSON web token has expired.",
                    SecurityTokenNotYetValidException => "The JSON web token is not yet valid.",
                    SecurityTokenInvalidAudienceException => "The JSON web token has an incorrect audience.",
                    SecurityTokenInvalidSignatureException => "The JSON web token has an invalid signature.",
                    _ => "The JSON web token could not be validated.",
                };

                _logger.LogInformation(validationException, "The JSON web token with recognized issuer {Issuer} could not be validated.", context.IssuerType);
            }

            return context;
        }
    }
}
