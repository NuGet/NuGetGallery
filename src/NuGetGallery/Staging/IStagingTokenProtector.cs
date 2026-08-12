// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Protects opaque staging continuation-token payloads so callers cannot inspect or forge the embedded owner,
    /// filter, and database keys. The default implementation uses the same ASP.NET <c>MachineKey</c> data protection
    /// that Gallery already relies on for tamper-proof cookies, and a focused abstraction keeps it substitutable
    /// under test.
    /// </summary>
    public interface IStagingTokenProtector
    {
        /// <summary>
        /// Signs and encrypts <paramref name="payload"/>, returning a URL-safe protected string.
        /// </summary>
        string Protect(byte[] payload);

        /// <summary>
        /// Verifies and decrypts <paramref name="protectedValue"/>. Returns <c>null</c> when the value is malformed,
        /// tampered, or was produced with a different key or purpose.
        /// </summary>
        byte[] Unprotect(string protectedValue);
    }
}
