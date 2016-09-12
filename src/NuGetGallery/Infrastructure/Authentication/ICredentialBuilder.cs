// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Infrastructure.Authentication
{
    public interface ICredentialBuilder
    {
        Credential CreatePasswordCredential(string plaintextPassword);

        Credential CreateApiKey(TimeSpan? expiration);

        Credential CreateExternalCredential(string issuer, string value, string identity);

        Credential ParseApiKeyCredential(string apiKey);
    }
}
