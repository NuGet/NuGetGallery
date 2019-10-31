// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Authentication
{
    public interface IAuthenticationService
    {
        /// <summary>
        /// Remove a credential from a user
        /// </summary>
        /// <param name="user">User to remove credential from</param>
        /// <param name="cred">Credential to remove</param>
        /// <param name="commitChanges">Default true. Commits changes immediately if true.</param>
        /// <returns>Returns a task that will complete when the credential has succesfully been removed.</returns>
        Task RemoveCredential(User user, Credential cred, bool commitChanges = true);

        /// <summary>
        /// Create the CredentialViewModel given the credential
        /// </summary>
        /// <returns>Returns the CredentialViewModel given the credential.</returns>
        CredentialViewModel DescribeCredential(Credential credential);

        /// <summary>
        /// Get the ApiKey credential given the apiKey
        /// </summary>
        /// <returns>Return a credential if there is a matched apiKey or null if there is not.</returns>
        Credential GetApiKeyCredential(string apiKey);
    }
}