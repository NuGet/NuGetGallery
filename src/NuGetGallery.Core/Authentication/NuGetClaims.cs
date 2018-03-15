// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public static class NuGetClaims
    {
        private const string ClaimsDomain = "https://claims.nuget.org/";

        /// <summary>
        /// The claim url for the claim that stores the value of the <see cref="CredentialTypes.ApiKey"/> that was used to authenticate the request.
        /// </summary>
        public const string ApiKey = ClaimsDomain + "apikey";

        /// <summary>
        /// The claim url for the claim that stores the serialized set of <see cref="NuGetGallery.Scope"/>s that the current <see cref="CredentialTypes.ApiKey"/> has access to.
        /// </summary>
        public const string Scope = ClaimsDomain + "scope";
        
        /// <summary>
        /// The claim url for the claim that stores the <see cref="Credential.Key"/> of the <see cref="Credential"/> used to authenticate the request.
        /// </summary>
        public const string CredentialKey = ClaimsDomain + "credentialkey";

        /// <summary>
        /// The claim url for the claim that stores whether or not the user is authenticated with a discontinued <see cref="Credential"/>.
        /// </summary>
        public const string DiscontinuedLogin = ClaimsDomain + "discontinuedlogin";

        /// <summary>
        /// The claim url for the claim that stores whether or not the user has a password login.
        /// </summary>
        public const string HasPasswordLogin = ClaimsDomain + "passwordlogin";

        /// <summary>
        /// The claim url for the claim that stores whether or not the user has an external login.
        /// </summary>
        public const string HasExternalLogin = ClaimsDomain + "externallogin";

        /// <summary>
        /// The default string value for all the boolean claims in <see cref="NuGetClaims"/>.
        /// </summary>
        public const string DefaultValue = "true";
    }
}
