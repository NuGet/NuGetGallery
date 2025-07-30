// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Diagnostics;

namespace NuGetGallery.Services.Authentication
{
    public enum FederatedCredentialPolicyResultType
    {
        Success, // policy matched token
        Unauthorized, // details are hidden from the user
        NotApplicable, // policy does not apply to the token
    }

    /// <summary>
    /// Represents the result of evaluating a single <see cref="NuGet.Services.Entities.FederatedCredentialPolicy">
    /// against validated and authenticated OIDC token.
    /// </summary>
    [DebuggerDisplay("{Type}, error: {Error}")]
    public class FederatedCredentialPolicyResult
    {
        public static readonly FederatedCredentialPolicyResult Success = new(FederatedCredentialPolicyResultType.Success, null, false);
        public static readonly FederatedCredentialPolicyResult NotApplicable = new(FederatedCredentialPolicyResultType.NotApplicable, null, false);

        private FederatedCredentialPolicyResult(
            FederatedCredentialPolicyResultType type,
            string? error, bool isErrorDisclosable)
        {
            Type = type;
            Error = error;
            IsErrorDisclosable = isErrorDisclosable;
        }

        public FederatedCredentialPolicyResultType Type { get; }
        public string? Error { get; }
        public bool IsErrorDisclosable { get; }

        public static FederatedCredentialPolicyResult Unauthorized(string error, bool isErrorDisclosable = false)
            => new(FederatedCredentialPolicyResultType.Unauthorized, error, isErrorDisclosable);
    }
}
