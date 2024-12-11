// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGetGallery.Services.Authentication
{
    /// <summary>
    /// A validator for federated credentials, in addition to any built-in validations.
    /// </summary>
    public interface IFederatedCredentialValidator
    {
        /// <summary>
        /// Validate the request headers of a given federated credential type.
        /// </summary>
        /// <param name="requestHeaders">The request headers containing the federated credential.</param>
        /// <param name="issuer">The detected issuer type.</param>
        /// <param name="unvalidatedClaims">
        /// The claims provided by the federated credential.
        /// It is the responsiblity of this method to validate claims before using them.</param>
        Task<FederatedCredentialValidation> ValidateAsync(
            NameValueCollection requestHeaders,
            FederatedCredentialIssuerType issuer,
            IEnumerable<Claim>? unvalidatedClaims);
    }
}
