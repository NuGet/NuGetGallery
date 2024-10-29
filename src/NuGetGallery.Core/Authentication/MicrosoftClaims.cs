// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public static class MicrosoftClaims
    {
        private const string ClaimsDomain = "http://schemas.microsoft.com/identity/claims/";

        /// <summary>
        /// The claim URL for the claim that stores the user's tenant ID, based on the external credential.
        /// </summary>
        public const string TenantId = ClaimsDomain + "tenantid";
    }
}
