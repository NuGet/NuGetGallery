// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// The types of federated credentials criteria that can be used to validate external credentials. This
    /// enum is used for the <see cref="FederatedCredentialPolicy.Type"/> property.
    /// </summary>
    public enum FederatedCredentialType
    {
        /// <summary>
        /// This credential type applies to Microsoft Entra ID OpenID Connect (OIDC) tokens, issued for a specific
        /// service principal. The service principal is identified by a tenant (directory ID) and an object ID (object
        /// ID). The application (client) ID is not used because the object ID uniquely identifies the service principal
        /// within the tenant. An object ID is required to show the service principal is provisioned within the tenant.
        ///
        /// Additional validation is done on the token claims which are the same for all Entra ID tokens, such as
        /// subject and expiration claims.
        /// </summary>
        EntraIdServicePrincipal = 1,

        /// <summary>
        /// This credential type applies to GitHub Actions workflows running in GitHub repositories. The workflow
        /// is identified by repository owner, repository name, workflow file, etc.
        /// </summary>
        GitHubActions = 2,
    }
}
