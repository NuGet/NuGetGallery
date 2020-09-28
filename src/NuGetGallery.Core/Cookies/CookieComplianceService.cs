// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Cookies
{
    public static class CookieComplianceService
    {
        public static ICookieComplianceService Instance;
        public static ILogger Logger;

        public static void Initialize(ICookieComplianceService cookieComplianceService, ILogger logger)
        {
            Instance = cookieComplianceService ?? throw new ArgumentNullException(nameof(cookieComplianceService));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}