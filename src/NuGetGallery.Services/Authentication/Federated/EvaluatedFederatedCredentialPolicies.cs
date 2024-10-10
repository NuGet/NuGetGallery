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
        private readonly FederatedCredentialPolicy? _policy;
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
            _policy = policy;
            _federatedCredential = federatedCredential;
        }

        public EvaluatedFederatedCredentialPoliciesType Type { get; }
        public string UserError => _userError ?? throw new InvalidOperationException();
        public IReadOnlyList<FederatedCredentialPolicyResult> Results => _results ?? throw new InvalidOperationException();
        public FederatedCredentialPolicy Policy => _policy ?? throw new InvalidOperationException();
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();

        public static EvaluatedFederatedCredentialPolicies BadToken(string userError)
            => new(EvaluatedFederatedCredentialPoliciesType.BadToken, userError);

        public static EvaluatedFederatedCredentialPolicies MatchedPolicy(IReadOnlyList<FederatedCredentialPolicyResult> results, FederatedCredentialPolicy policy, FederatedCredential federatedCredential)
            => new(EvaluatedFederatedCredentialPoliciesType.MatchedPolicy, userError: null, results, policy, federatedCredential);

        public static EvaluatedFederatedCredentialPolicies NoMatchingPolicy(IReadOnlyList<FederatedCredentialPolicyResult> results)
            => new(EvaluatedFederatedCredentialPoliciesType.NoMatchingPolicy, userError: null, results);
    }

    public enum FederatedCredentialPolicyResultType
    {
        Success, // policy matched token
        Unauthorized, // details are hidden from the user
    }

    public class FederatedCredentialPolicyResult
    {
        private readonly FederatedCredential? _federatedCredential;
        private readonly string? _internalReason;

        public FederatedCredentialPolicyResult(
            FederatedCredentialPolicyResultType type,
            FederatedCredentialPolicy federatedCredentialPolicy,
            FederatedCredential? federatedCredential = null,
            string? internalReason = null)
        {
            Type = type;
            Policy = federatedCredentialPolicy;
            _federatedCredential = federatedCredential;
            _internalReason = internalReason;
        }

        public FederatedCredentialPolicyResultType Type { get; }
        public FederatedCredentialPolicy Policy { get; }
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();
        public string InternalReason => _internalReason ?? throw new InvalidOperationException();

        public static FederatedCredentialPolicyResult Success(FederatedCredentialPolicy policy, FederatedCredential federatedCredential)
            => new(FederatedCredentialPolicyResultType.Success, policy, federatedCredential: federatedCredential);

        public static FederatedCredentialPolicyResult Unauthorized(FederatedCredentialPolicy policy, string internalReason)
            => new(FederatedCredentialPolicyResultType.Unauthorized, policy, internalReason: internalReason);
    }
}
