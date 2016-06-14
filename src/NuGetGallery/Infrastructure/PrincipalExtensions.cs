// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Principal;

namespace NuGetGallery
{
    public static class PrincipalExtensions
    {
        public static bool IsAdministrator(this IPrincipal self)
        {
            return self.IsInRole(Constants.AdminRoleName);
        }

        public static bool IsDelegated(this IPrincipal self)
        {
            return self.IsInRole(Constants.DelegatedRoleName);
        }
    }
}