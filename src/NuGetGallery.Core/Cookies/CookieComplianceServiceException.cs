// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Cookies
{
    public class CookieComplianceServiceException : Exception
    {
        public CookieComplianceServiceException(Exception inner)
            : base("The cookie compliance service threw an exception!", inner)
        {
        }
    }
}
