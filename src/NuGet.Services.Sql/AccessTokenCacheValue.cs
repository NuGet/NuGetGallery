// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Identity.Client;

namespace NuGet.Services.Sql
{
    internal class AccessTokenCacheValue
    {
        public AccessTokenCacheValue(string clientCertificateData, AuthenticationResult authenticationResult)
        {
            ClientCertificateData = clientCertificateData;
            AuthenticationResult = new AuthenticationResultWrapper(authenticationResult);
        }

        public AccessTokenCacheValue(string clientCertificateData, IAuthenticationResult authenticationResult)
        {
            ClientCertificateData = clientCertificateData;
            AuthenticationResult = authenticationResult;
        }

        public string ClientCertificateData { get; }

        public IAuthenticationResult AuthenticationResult { get; }
    }
}