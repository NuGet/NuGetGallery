// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public static class AuthenticationTypes
    {
        public static readonly string External = "External";
        public static readonly string LocalUser = "LocalUser";
        public static readonly string ApiKey = "ApiKey";
    }
}