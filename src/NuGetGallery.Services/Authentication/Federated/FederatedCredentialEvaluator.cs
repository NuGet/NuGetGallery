// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using NuGet.Services.Entities;
using static NuGetGallery.Services.Authentication.FederatedCredentialPolicyResult;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialEvaluator
    {
        Task<EvaluatedFederatedCredentialPolicies> GetMatchingPolicyAsync(IReadOnlyCollection<FederatedCredentialPolicy> policies, string bearerToken);
    }

    public class FederatedCredentialEvaluator : IFederatedCredentialEvaluator
    {
        private readonly IEntraIdTokenValidator _entraIdTokenValidator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<FederatedCredentialEvaluator> _logger;

        public FederatedCredentialEvaluator(
            IEntraIdTokenValidator entraIdTokenValidator,
            IDateTimeProvider dateTimeProvider,
            ILogger<FederatedCredentialEvaluator> logger)
        {
            _entraIdTokenValidator = entraIdTokenValidator ?? throw new ArgumentNullException(nameof(entraIdTokenValidator));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EvaluatedFederatedCredentialPolicies> GetMatchingPolicyAsync(IReadOnlyCollection<FederatedCredentialPolicy> policies, string bearerToken)
        {
            var results = new List<FederatedCredentialPolicyResult>();
            FederatedCredentialPolicyResult? selectedResult = null;

            // sort the policy results by creation date, so older policy results are preferred
            foreach (var policy in policies.OrderBy(x => x.Created))
            {
                var result = policy.Type switch
                {
                    FederatedCredentialType.EntraIdServicePrincipal => await EvaluateEntraIdServicePrincipalToken(policy, bearerToken),
                    _ => throw new NotImplementedException($"Unsupported federated credential type: {policy.Type}"),
                };

                string reason;
                if (result.Type == FederatedCredentialPolicyResultType.Success)
                {
                    selectedResult = result;
                    reason = "policy matched token";
                }
                else
                {
                    reason = result.Reason;
                }

                _logger.LogInformation(
                    "Evaluated policy key {PolicyKey} of type {PolicyType}. Result type: {ResultType}. Reason: {Reason}",
                    result.Policy.Key,
                    result.Policy.Type,
                    result.Type,
                    reason);

                results.Add(result);

                if (selectedResult is not null)
                {
                    break;
                }
            }

            return new EvaluatedFederatedCredentialPolicies(
                results,
                selectedResult?.Policy,
                selectedResult?.FederatedCredential);
        }

        private async Task<FederatedCredentialPolicyResult> EvaluateEntraIdServicePrincipalToken(FederatedCredentialPolicy policy, string bearerToken)
        {
            JsonWebToken jwt;
            try
            {
                jwt = new JsonWebToken(bearerToken);
            }
            catch (ArgumentException ex)
            {
                return BadTokenFormat(policy, ex.Message);
            }

            var tokenValidationResult = await _entraIdTokenValidator.ValidateAsync(jwt);
            if (!tokenValidationResult.IsValid)
            {
                var internalMessage = tokenValidationResult.Exception?.Message ?? "The JSON web token is not valid.";
                return Unacceptable(policy, internalMessage);
            }

            if (jwt.Audiences.Count() != 1)
            {
                return InvalidClaim(policy, "The JSON web token must have exactly one audience.");
            }

            // See https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference
            if (!jwt.TryGetPayloadValue<string>("tid", out var tid)) // tenant (directory) ID
            {
                return InvalidClaim(policy, "The JSON web token is missing a tid claim.");
            }

            if (!jwt.TryGetPayloadValue<string>("oid", out var oid)) // object ID
            {
                return InvalidClaim(policy, "The JSON web token is missing a oid claim.");
            }

            if (!jwt.TryGetPayloadValue<string>("uti", out var uti)) // unique token identifier (equivalent to jti)
            {
                return InvalidClaim(policy, "The JSON web token is missing a uti claim.");
            }

            if (!jwt.TryGetPayloadValue<string>("azpacr", out var azpacr)) // client credential type
            {
                return InvalidClaim(policy, "The JSON web token is missing a azpacr claim.");
            }

            if (string.IsNullOrWhiteSpace(uti))
            {
                return InvalidClaim(policy, "The JSON web token has an empty uti claim.");
            }

            // 2 indicates a client certificate was used
            if (azpacr != "2")
            {
                return InvalidClaim(policy, "The JSON web token must have an azpacr claim with a value of 2.");
            }

            if (jwt.Subject != oid)
            {
                return InvalidClaim(policy, "The JSON web token sub claim must match the oid claim.");
            }

            var criteria = DeserializePolicy<EntraIdServicePrincipalCriteria>(policy);

            if (string.IsNullOrWhiteSpace(tid) || !Guid.TryParse(tid, out var parsedTid) || parsedTid != criteria.TenantId)
            {
                return ParameterMismatch(policy, "The JSON web token must have a tid claim that matches the policy.");
            }

            if (string.IsNullOrWhiteSpace(oid) || !Guid.TryParse(oid, out var parsedOid) || parsedOid != criteria.ObjectId)
            {
                return ParameterMismatch(policy, "The JSON web token must have a oid claim that matches the policy.");
            }

            var federatedCredential = new FederatedCredential
            {
                Identity = uti,
                FederatedCredentialPolicyKey = policy.Key,
                Type = policy.Type,
                Created = _dateTimeProvider.UtcNow,
                Expires = jwt.ValidTo.ToUniversalTime(),
            };

            return Success(policy, federatedCredential);
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
