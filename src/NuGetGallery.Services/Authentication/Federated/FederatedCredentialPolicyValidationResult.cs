// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public enum FederatedCredentialPolicyValidationResultType
    {
        Success,
        BadRequest,
        Unauthorized,
    }

    [DebuggerDisplay("{Type}, error: {UserMessage}")]
    public class FederatedCredentialPolicyValidationResult
    {
        private readonly FederatedCredentialPolicy? _policy;

        private FederatedCredentialPolicyValidationResult(
            FederatedCredentialPolicyValidationResultType type,
            string? userMessage = null,
            string? policyPropertyName = null,
            FederatedCredentialPolicy? policy = null)
        {
            Type = type;
            UserMessage = userMessage;
            PolicyPropertyName = policyPropertyName;
            _policy = policy;
        }

        public FederatedCredentialPolicyValidationResultType Type { get; }

        /// <summary>
        /// Gets the user message associated with the current operation.
        /// </summary>
        public string? UserMessage { get; }

        /// <summary>
        /// Name of a <see cref="FederatedCredentialPolicy"/> property associated with <see cref="UserMessage">.
        /// </summary>
        public string? PolicyPropertyName { get; }

        public FederatedCredentialPolicy Policy => _policy ?? throw new InvalidOperationException();

        public static FederatedCredentialPolicyValidationResult Success(FederatedCredentialPolicy policy)
            => new(FederatedCredentialPolicyValidationResultType.Success, policy: policy);

        public static FederatedCredentialPolicyValidationResult BadRequest(string userMessage, string? policyPropertyName)
            => new FederatedCredentialPolicyValidationResult(FederatedCredentialPolicyValidationResultType.BadRequest, userMessage, policyPropertyName);

        public static FederatedCredentialPolicyValidationResult Unauthorized(string userMessage, string? policyPropertyName)
            => new FederatedCredentialPolicyValidationResult(FederatedCredentialPolicyValidationResultType.Unauthorized, userMessage, policyPropertyName);
    }
}
