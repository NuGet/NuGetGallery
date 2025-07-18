// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NuGet.Services.Entities;
using Serilog.Filters;

namespace NuGetGallery.Services.Authentication
{
    public enum OidcTokenEvaluationResultType
    {
        BadToken,
        MatchedPolicy,
        NoMatchingPolicy,
    }

    /// <summary>
    /// Represents the result of a complete evaluation of an incoming OIDC token.
    /// </summary>
    /// <remarks>
    /// The evaluation process includes:
    /// <para>• Validating standard token claims</para>
    /// <para>• Locating token handler, e.g. Entra ID, GitHub Actions</para>
    /// <para>• Authenticating the token</para>
    /// <para>• Matching against the user's configured <see cref="FederatedCredentialPolicy"/> entries</para>
    /// </remarks>
    [DebuggerDisplay("{Type}, error: {UserError}")]
    public class OidcTokenEvaluationResult
    {
        private readonly FederatedCredentialPolicy? _matchedPolicy;
        private readonly FederatedCredential? _federatedCredential;

        private OidcTokenEvaluationResult(
            OidcTokenEvaluationResultType type,
            string? userError = null,
            FederatedCredentialPolicy? policy = null,
            FederatedCredential? federatedCredential = null)
        {
            Type = type;
            UserError = userError;
            _matchedPolicy = policy;
            _federatedCredential = federatedCredential;
        }

        /// <summary>
        /// Type of evaluation result indicating whether the token was valid, matched a policy, or had no matching policy.
        /// </summary>
        public OidcTokenEvaluationResultType Type { get; }

        /// <summary>
        /// User-facing error message when evaluation fails. Null for successful
        /// results or when no disclosable error details are available.
        /// </summary>
        public string? UserError { get; }

        /// <summary>
        /// Federated credential policy that matched the incoming token.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when accessed and <see cref="Type"/> is not <see cref="OidcTokenEvaluationResultType.MatchedPolicy"/>.</exception>
        public FederatedCredentialPolicy MatchedPolicy => _matchedPolicy ?? throw new InvalidOperationException();

        /// <summary>
        /// Federated credential created from the matched policy and token.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when accessed and <see cref="Type"/> is not <see cref="OidcTokenEvaluationResultType.MatchedPolicy"/>.</exception>
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();

        public static OidcTokenEvaluationResult BadToken(string userError)
            => new(OidcTokenEvaluationResultType.BadToken, userError ?? throw new ArgumentNullException(nameof(userError)));

        public static OidcTokenEvaluationResult NewMatchedPolicy(FederatedCredentialPolicy matchedPolicy, FederatedCredential federatedCredential)
            => new(OidcTokenEvaluationResultType.MatchedPolicy, userError: null, matchedPolicy, federatedCredential);

        public static OidcTokenEvaluationResult NoMatchingPolicy(string? userError = null)
            => new(OidcTokenEvaluationResultType.NoMatchingPolicy, userError);
    }
}
