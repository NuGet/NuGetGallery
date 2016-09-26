// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public static class NuGetScopes
    {
        public const string All = "all";
        public const string PackagePush = "package:push";
        public const string PackagePushNew = "package:pushnew";
        public const string PackageList = "package:list";

        public static string Describe(string scope)
        {
            switch (scope.ToLowerInvariant())
            {
                case All:
                    return "All";
                case PackagePushNew:
                    return "Push new package registration";
                case PackagePush:
                    return "Push package version";
                case PackageList:
                    return "List/unlist package";
            }

            return "Unknown";
        }
    }
}