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
                    return Strings.ScopeDescription_All;
                case PackagePushNew:
                    return Strings.ScopeDescription_PushNewPackageRegistration;
                case PackagePush:
                    return Strings.ScopeDescription_PushPackageVersion;
                case PackageList:
                    return Strings.ScopeDescription_ListUnlistPackage;
            }

            return Strings.ScopeDescription_Unknown;
        }
    }
}