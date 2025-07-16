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
    [DebuggerDisplay("{Type}, error: {_userError}")]
    public class OidcTokenEvaluationResult
    {
        private readonly string? _userError;
        private readonly FederatedCredentialPolicy? _matchedPolicy;
        private readonly FederatedCredential? _federatedCredential;

        private OidcTokenEvaluationResult(
            OidcTokenEvaluationResultType type,
            string? userError = null,
            FederatedCredentialPolicy? policy = null,
            FederatedCredential? federatedCredential = null)
        {
            Type = type;
            _userError = userError;
            _matchedPolicy = policy;
            _federatedCredential = federatedCredential;
        }

        public OidcTokenEvaluationResultType Type { get; }
        public string UserError => _userError ?? throw new InvalidOperationException();
        public FederatedCredentialPolicy MatchedPolicy => _matchedPolicy ?? throw new InvalidOperationException();
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();

        public static OidcTokenEvaluationResult BadToken(string userError)
            => new(OidcTokenEvaluationResultType.BadToken, userError);

        public static OidcTokenEvaluationResult NewMatchedPolicy(FederatedCredentialPolicy matchedPolicy, FederatedCredential federatedCredential)
            => new(OidcTokenEvaluationResultType.MatchedPolicy, userError: null, matchedPolicy, federatedCredential);

        public static OidcTokenEvaluationResult NoMatchingPolicy()
            => new(OidcTokenEvaluationResultType.NoMatchingPolicy, userError: null);
    }
}
