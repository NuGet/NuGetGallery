// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public static class NuGetClaims
    {
        // Normally public consts are bad, but here we can't change the claim URL without messing
        // things up, so we should encourage that by using a const.
        public const string ApiKey = "https://claims.nuget.org/apikey";

        public const string Scope = "https://claims.nuget.org/scope";
    }
}