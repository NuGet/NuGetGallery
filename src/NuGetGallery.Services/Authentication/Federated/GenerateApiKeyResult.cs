// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public enum GenerateApiKeyResultType
    {
        Created,
        BadRequest,
        Unauthorized,
    }

    public class GenerateApiKeyResult
    {
        private readonly string? _userMessage;
        private readonly string? _plaintextApiKey;
        private readonly DateTimeOffset? _expires;

        private GenerateApiKeyResult(
            GenerateApiKeyResultType type,
            string? userMessage = null,
            string? plaintextApiKey = null,
            DateTimeOffset? expires = null)
        {
            Type = type;
            _userMessage = userMessage;
            _plaintextApiKey = plaintextApiKey;
            _expires = expires;
        }

        public GenerateApiKeyResultType Type { get; }
        public string UserMessage => _userMessage ?? throw new InvalidOperationException();
        public string PlaintextApiKey => _plaintextApiKey ?? throw new InvalidOperationException();
        public DateTimeOffset Expires => _expires ?? throw new InvalidOperationException();

        public static GenerateApiKeyResult Created(string plaintextApiKey, DateTimeOffset expires)
            => new(GenerateApiKeyResultType.Created, plaintextApiKey: plaintextApiKey, expires: expires);

        public static GenerateApiKeyResult BadRequest(string userMessage) => new(GenerateApiKeyResultType.BadRequest, userMessage);
        public static GenerateApiKeyResult Unauthorized(string userMessage) => new(GenerateApiKeyResultType.Unauthorized, userMessage);
    }
}
