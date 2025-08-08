// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Infrastructure.Authentication
{
    public interface ICredentialBuilder
    {
        Credential CreatePasswordCredential(string plaintextPassword);

        Credential CreateApiKey(TimeSpan? expiration, out string plaintextApiKey);

        Credential CreatePackageVerificationApiKey(Credential originalApiKey, string id);

        Credential CreateExternalCredential(string issuer, string value, string identity, string tenantId = null);

        IList<Scope> BuildScopes(User scopeOwner, string[] scopes, string[] subjects);

        bool VerifyScopes(User currentUser, IEnumerable<Scope> scopes);

        Credential CreateShortLivedApiKey(TimeSpan expiration, FederatedCredentialPolicy policy, string galleryEnvironment, out string plaintextApiKey);
    }
}
