// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    /// <summary>
    /// Provides centralized validation and evaluation logic for federated identity scenarios, such as Entra ID and GitHub Actions:
    /// <para> * <see cref="FederatedCredentialPolicy"/> validation during creation or update. </para>
    /// <para> * <see cref="JsonWebToken"/> OIDC token authentication. </para>
    /// <para> * Token-to-policy matching. </para>
    /// </summary>
    public interface ITokenPolicyValidator
    {
        /// <summary>
        /// The <see cref="System.Uri.Authority"/> part of the issues URI that this validator supports.
        /// Used to match incoming tokens to the correct validator.
        /// </summary>
        string IssuerAuthority { get; }

        /// <summary>
        /// The <see cref="FederatedCredentialIssuerType"/> that this validator supports.
        /// </summary>
        FederatedCredentialIssuerType IssuerType { get; }

        /// <summary>
        /// Validates <see cref="FederatedCredentialPolicy"/> during its creation or update.
        /// </summary>
        /// <remarks>
        /// NOTE that validation may adjust policy fields to ensure consistency, e.g. validating
        /// a temporary GitHub Actions policy will ensure correct "validate by" date.
        /// </remarks>
        FederatedCredentialPolicyValidationResult ValidatePolicy(FederatedCredentialPolicy policy);

        /// <summary>
        /// Validates the token identifier of the specified JSON Web Token.
        /// </summary>
        /// <param name="jwt">The JSON Web Token to validate.</param>
        /// <returns>A tuple containing the token identifier and an error message.  The <c>tokenId</c> is the validated token
        /// identifier, or <see langword="null"/> if validation fails. The <c>error</c> is a description of the
        /// validation error, or <see langword="null"/> if validation is successful.</returns>
        public (string? tokenId, string? error) ValidateTokenIdentifier(JsonWebToken jwt);

        /// <summary>
        /// Validates a JSON Web Token (JWT) based on the issuer, audience, and signature expected by this validator (e.g., Entra ID or GitHub Actions).
        /// </summary>
        /// <param name="jwt">The JSON Web Token to validate.</param>
        Task<TokenValidationResult> ValidateTokenAsync(JsonWebToken jwt);

        /// <summary>
        /// Evaluates the specified federated credential policy against a JSON Web Token (JWT).
        /// The token must have been validated by <see cref="ValidateTokenAsync(JsonWebToken)"/> before calling this method.
        /// </summary>
        /// <param name="policy">The federated credential policy to evaluate.</param>
        /// <param name="jwt">The JSON Web Token to be evaluated against the policy.</param>
        Task<FederatedCredentialPolicyResult> EvaluatePolicyAsync(FederatedCredentialPolicy policy, JsonWebToken jwt);
    }
}
