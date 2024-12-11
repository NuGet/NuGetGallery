// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Authentication
{
    public enum EvaluatedFederatedCredentialPoliciesType
    {
        BadToken,
        MatchedPolicy,
        NoMatchingPolicy,
    }

    public class EvaluatedFederatedCredentialPolicies
    {
        private readonly string? _userError;
        private readonly IReadOnlyList<FederatedCredentialPolicyResult>? _results;
        private readonly FederatedCredentialPolicy? _matchedPolicy;
        private readonly FederatedCredential? _federatedCredential;

        private EvaluatedFederatedCredentialPolicies(
            EvaluatedFederatedCredentialPoliciesType type,
            string? userError = null,
            IReadOnlyList<FederatedCredentialPolicyResult>? results = null,
            FederatedCredentialPolicy? policy = null,
            FederatedCredential? federatedCredential = null)
        {
            Type = type;
            _userError = userError;
            _results = results;
            _matchedPolicy = policy;
            _federatedCredential = federatedCredential;
        }

        public EvaluatedFederatedCredentialPoliciesType Type { get; }
        public string UserError => _userError ?? throw new InvalidOperationException();
        public IReadOnlyList<FederatedCredentialPolicyResult> Results => _results ?? throw new InvalidOperationException();
        public FederatedCredentialPolicy MatchedPolicy => _matchedPolicy ?? throw new InvalidOperationException();
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();

        public static EvaluatedFederatedCredentialPolicies BadToken(string userError)
            => new(EvaluatedFederatedCredentialPoliciesType.BadToken, userError);

        public static EvaluatedFederatedCredentialPolicies NewMatchedPolicy(IReadOnlyList<FederatedCredentialPolicyResult> results, FederatedCredentialPolicy matchedPolicy, FederatedCredential federatedCredential)
            => new(EvaluatedFederatedCredentialPoliciesType.MatchedPolicy, userError: null, results, matchedPolicy, federatedCredential);

        public static EvaluatedFederatedCredentialPolicies NoMatchingPolicy(IReadOnlyList<FederatedCredentialPolicyResult> results)
            => new(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, userError: null, results);
    }
}
