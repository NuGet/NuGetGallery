// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.Infrastructure.Authentication
{
    public interface ICredentialValidator
    {
        /// <summary>
        /// Validates the provided password matches the credential.
        /// </summary>
        bool ValidatePasswordCredential(Credential credential, string providedPassword);

        /// <summary>
        /// Validates the provided ApiKey exists and valid.
        /// </summary>
        /// <param name="allCredentials">An <see cref="IQueryable"/> with of all credentials.</param>
        /// <param name="providedApiKey">User provided ApiKey</param>
        /// <returns>List of matching ApiKeys. If only a single result is expected it's up to the caller to validate count.</returns>
        IList<Credential> GetValidCredentialsForApiKey(IQueryable<Credential> allCredentials, string providedApiKey);
    }
}
