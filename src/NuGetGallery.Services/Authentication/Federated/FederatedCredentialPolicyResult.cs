// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.Authentication
{
    public enum FederatedCredentialPolicyResultType
    {
        Success, // policy matched token
        Unauthorized, // details are hidden from the user
    }

    public class FederatedCredentialPolicyResult
    {
        private readonly FederatedCredential? _federatedCredential;
        private readonly string? _internalReason;

        private FederatedCredentialPolicyResult(
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
