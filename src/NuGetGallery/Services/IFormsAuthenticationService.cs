// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Web;

namespace NuGetGallery
{
    public interface IFormsAuthenticationService
    {
        void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles);

        void SignOut();

        bool ShouldForceSSL(HttpContextBase context);
    }
}