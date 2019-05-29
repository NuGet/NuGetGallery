// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.Authentication
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
                    return Strings.ScopeDescription_All;
                case PackagePush:
                    return Strings.ScopeDescription_PushPackage;
                case PackagePushVersion:
                    return Strings.ScopeDescription_PushPackageVersion;
                case PackageUnlist:
                    return Strings.ScopeDescription_UnlistPackage;
                case PackageVerify:
                    return Strings.ScopeDescription_VerifyPackage;
            }

            return Strings.ScopeDescription_Unknown;
        }
    }
}