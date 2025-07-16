// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Diagnostics;

namespace NuGetGallery.Services.Authentication
{
    public enum FederatedCredentialPolicyResultType
    {
        Success, // policy matched token
        NotApplicable, // policy does not apply to the token
        Unauthorized, // details are hidden from the user
    }

    /// <summary>
    /// Represents the result of evaluating a single <see cref="NuGet.Services.Entities.FederatedCredentialPolicy">
    /// against validated and authenticated OIDC token.
    /// </summary>
    [DebuggerDisplay("{Type}, reason: {InternalReason}")]
    public class FederatedCredentialPolicyResult
    {
        public static readonly FederatedCredentialPolicyResult Success = new(FederatedCredentialPolicyResultType.Success);
        public static readonly FederatedCredentialPolicyResult NotApplicable = new(FederatedCredentialPolicyResultType.NotApplicable);

        private FederatedCredentialPolicyResult(
            FederatedCredentialPolicyResultType type,
            string? internalReason = null)
        {
            Type = type;
            InternalReason = internalReason ?? string.Empty;
        }

        public FederatedCredentialPolicyResultType Type { get; }
        public string InternalReason { get; }

        public static FederatedCredentialPolicyResult Unauthorized(string internalReason)
            => new(FederatedCredentialPolicyResultType.Unauthorized, internalReason: internalReason);
    }
}
