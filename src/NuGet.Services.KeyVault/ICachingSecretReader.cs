// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.KeyVault
{
    public interface ICachingSecretReader : ISecretReader
    {
        bool TryGetCachedSecret(string secretName, out string secretValue);
        bool TryGetCachedSecret(string secretName, ILogger logger, out string secretValue);
        bool TryGetCachedSecretObject(string secretName, out ISecret secretObject);
        bool TryGetCachedSecretObject(string secretName, ILogger logger, out ISecret secretObject);
    }
}
