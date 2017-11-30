// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGetGallery
{
    /// <summary>
    /// APIs that provide lightweight extensibility for the User entity.
    /// </summary>
    public static class UserExtensions
    {
        public static bool IsAdministrator(this User self)
        {
            return self.IsInRole(Constants.AdminRoleName);
        }
    }
}