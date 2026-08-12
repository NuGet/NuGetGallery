// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Security;

namespace NuGetGallery
{
    /// <summary>
    /// The production <see cref="IStagingTokenProtector"/>. It uses ASP.NET <see cref="MachineKey"/> data protection,
    /// matching the tamper-proof cookie handling already used by <c>CookieTempDataProvider</c>, so continuation tokens
    /// are authenticated and encrypted rather than plain base64 that a caller could inspect or forge.
    /// </summary>
    public class MachineKeyStagingTokenProtector : IStagingTokenProtector
    {
        private const string Purpose = "NuGetGallery.Staging.ContinuationToken.v1";

        public string Protect(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return HttpServerUtility.UrlTokenEncode(MachineKey.Protect(payload, Purpose));
        }

        public byte[] Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return null;
            }

            try
            {
                var protectedBytes = HttpServerUtility.UrlTokenDecode(protectedValue);
                return protectedBytes == null ? null : MachineKey.Unprotect(protectedBytes, Purpose);
            }
            catch (Exception ex) when (ex is FormatException || ex is System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }
}
