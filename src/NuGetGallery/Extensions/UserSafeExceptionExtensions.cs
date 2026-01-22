// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public static class UserSafeExceptionExtensions
    {
        public static void Log(this Exception self)
        {
            IUserSafeException uvex = self as IUserSafeException;
            if (uvex != null)
            {
                // Log the exception that the User-Visible wrapper marked as to-be-logged
                QuietLog.LogHandledException(uvex.LoggedException);
            }
            else
            {
                QuietLog.LogHandledException(self);
            }
        }
    }
}
