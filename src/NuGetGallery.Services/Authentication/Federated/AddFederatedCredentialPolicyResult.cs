// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public enum AddFederatedCredentialPolicyResultType
    {
        Created,
        BadRequest,
    }

    public class AddFederatedCredentialPolicyResult
    {
        private readonly string? _userMessage;
        private readonly FederatedCredentialPolicy? _policy;

        private AddFederatedCredentialPolicyResult(
            AddFederatedCredentialPolicyResultType type,
            string? userMessage = null,
            FederatedCredentialPolicy? policy = null)
        {
            Type = type;
            _userMessage = userMessage;
            _policy = policy;
        }

        public AddFederatedCredentialPolicyResultType Type { get; }
        public string UserMessage => _userMessage ?? throw new InvalidOperationException();
        public FederatedCredentialPolicy Policy => _policy ?? throw new InvalidOperationException();

        public static AddFederatedCredentialPolicyResult Created(FederatedCredentialPolicy policy)
            => new(AddFederatedCredentialPolicyResultType.Created, policy: policy);

        public static AddFederatedCredentialPolicyResult BadRequest(string userMessage)
            => new AddFederatedCredentialPolicyResult(AddFederatedCredentialPolicyResultType.BadRequest, userMessage);
    }
}
