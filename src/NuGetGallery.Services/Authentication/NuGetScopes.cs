// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public static class NuGetScopes
    {
        public const string All = "all";
        public const string PackagePushVersion = "package:pushversion";
        public const string PackagePush = "package:push";
        public const string PackageUnlist = "package:unlist";
        public const string PackageVerify = "package:verify";

        public static string Describe(string scope)
        {
            switch (scope.ToLowerInvariant())
            {
                case All:
                    return ServicesStrings.ScopeDescription_All;
                case PackagePush:
                    return ServicesStrings.ScopeDescription_PushPackage;
                case PackagePushVersion:
                    return ServicesStrings.ScopeDescription_PushPackageVersion;
                case PackageUnlist:
                    return ServicesStrings.ScopeDescription_UnlistPackage;
                case PackageVerify:
                    return ServicesStrings.ScopeDescription_VerifyPackage;
            }

            return ServicesStrings.ScopeDescription_Unknown;
        }
    }
}