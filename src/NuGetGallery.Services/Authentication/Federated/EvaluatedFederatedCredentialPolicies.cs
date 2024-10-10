// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Authentication
{
    public class EvaluatedFederatedCredentialPolicies
    {
        private readonly FederatedCredentialPolicy? _selectedPolicy;
        private readonly FederatedCredential? _federatedCredential;

        public EvaluatedFederatedCredentialPolicies(
            IReadOnlyList<FederatedCredentialPolicyResult> results,
            FederatedCredentialPolicy? selectedPolicy,
            FederatedCredential? federatedCredential)
        {
            Results = results;
            _selectedPolicy = selectedPolicy;
            _federatedCredential = federatedCredential;

            if ((selectedPolicy is null) != (federatedCredential is null))
            {
                throw new ArgumentException("Either both selected policy and federated credential must be null or neither can be null.");
            }
        }

        public IReadOnlyList<FederatedCredentialPolicyResult> Results { get; }
        public bool HasMatchingPolicy => _selectedPolicy is not null;
        public FederatedCredentialPolicy SelectedPolicy => _selectedPolicy ?? throw new InvalidOperationException();
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();
    }

    public enum FederatedCredentialPolicyResultType
    {
        Success,
        BadFormat,
        Unacceptable,
        InvalidClaim,
        ParameterMismatch,
    }

    public class FederatedCredentialPolicyResult
    {
        private readonly string? _reason;
        private readonly FederatedCredential? _federatedCredential;

        public FederatedCredentialPolicyResult(
            FederatedCredentialPolicyResultType type,
            FederatedCredentialPolicy federatedCredentialPolicy,
            string? reason,
            FederatedCredential? federatedCredential)
        {
            Type = type;
            Policy = federatedCredentialPolicy;
            _reason = reason;
            _federatedCredential = federatedCredential;
        }

        public FederatedCredentialPolicyResultType Type { get; }
        public FederatedCredentialPolicy Policy { get; }
        public string Reason => _reason ?? throw new InvalidOperationException();
        public FederatedCredential FederatedCredential => _federatedCredential ?? throw new InvalidOperationException();

        public static FederatedCredentialPolicyResult Success(FederatedCredentialPolicy policy, FederatedCredential federatedCredential)
            => new(FederatedCredentialPolicyResultType.Success, policy, reason: null, federatedCredential);

        public static FederatedCredentialPolicyResult BadTokenFormat(FederatedCredentialPolicy policy, string reason)
            => new(FederatedCredentialPolicyResultType.BadFormat, policy, reason, federatedCredential: null);

        public static FederatedCredentialPolicyResult Unacceptable(FederatedCredentialPolicy policy, string reason)
            => new(FederatedCredentialPolicyResultType.Unacceptable, policy, reason, federatedCredential: null);

        public static FederatedCredentialPolicyResult InvalidClaim(FederatedCredentialPolicy policy, string reason)
            => new(FederatedCredentialPolicyResultType.InvalidClaim, policy, reason, federatedCredential: null);

        public static FederatedCredentialPolicyResult ParameterMismatch(FederatedCredentialPolicy policy, string reason)
            => new(FederatedCredentialPolicyResultType.ParameterMismatch, policy, reason, federatedCredential: null);
    }
}
