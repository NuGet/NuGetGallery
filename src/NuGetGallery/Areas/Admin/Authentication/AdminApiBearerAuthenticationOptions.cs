// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin.Security;

namespace NuGetGallery.Areas.Admin.Authentication
{
    public class AdminApiBearerAuthenticationOptions : AuthenticationOptions
    {
        public const string DefaultAuthenticationType = "AdminApiBearer";

        /// <summary>
        /// OWIN environment key used to pass authentication error details from the
        /// handler to the MVC authorization filter.
        /// </summary>
        internal const string AuthErrorEnvironmentKey = "AdminApi.AuthError";

        public AdminApiBearerAuthenticationOptions()
            : base(DefaultAuthenticationType)
        {
            AuthenticationMode = AuthenticationMode.Active;
        }
    }
}
