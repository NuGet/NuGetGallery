// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.Validation.Issues
{
    public class ClientSigningVerificationFailure : ValidationIssue
    {
        private readonly string _codeAndMessage;

        [JsonConstructor]
        public ClientSigningVerificationFailure(string clientCode, string clientMessage)
        {
            ClientCode = clientCode ?? throw new ArgumentNullException(nameof(clientCode));
            ClientMessage = clientMessage ?? throw new ArgumentNullException(nameof(clientMessage));
            _codeAndMessage = $"{ClientCode}: {ClientMessage}";
        }

        public override ValidationIssueCode IssueCode => ValidationIssueCode.ClientSigningVerificationFailure;

        [JsonProperty("c", Required = Required.Always)]
        public string ClientCode { get; }
        
        [JsonProperty("m", Required = Required.Always)]
        public string ClientMessage { get; }

        public override string GetMessage() => _codeAndMessage;
    }
}
