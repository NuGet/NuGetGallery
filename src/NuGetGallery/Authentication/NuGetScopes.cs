﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

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

        public static bool IsActionAllowedOnSubjectByOwner(PackageRegistration packageRegistration, User owner, params string[] requestedActions)
        {
            if (packageRegistration == null)
            {
                return true;
            }

            return requestedActions
                .Select(GetRequiredPackageAction)
                .Any(p => PermissionsService.IsActionAllowed(packageRegistration, owner, p));
        }

        private static PermissionLevel GetRequiredPackageAction(string scope)
        {
            switch (scope.ToLowerInvariant())
            {
                case All:
                    return ApiActions.ApiAll;
                case PackagePush:
                case PackagePushVersion:
                    return ApiActions.ApiPush;
                case PackageUnlist:
                    return ApiActions.ApiUnlist;
                case PackageVerify:
                    return ApiActions.ApiVerify;
                default:
                    return PermissionLevel.Anonymous;
            }
        }

        public static bool IsActionAllowedOnOwnerByCurrentUser(User owner, User currentUser, params string[] requestedActions)
        {
            return requestedActions
                .Select(GetRequiredAccountAction)
                .Any(p => PermissionsService.IsActionAllowed(owner, currentUser, p));
        }

        private static PermissionLevel GetRequiredAccountAction(string scope)
        {
            switch (scope.ToLowerInvariant())
            {
                case All:
                    return ApiActions.ApiAllOnBehalfOf;
                case PackagePush:
                    return ApiActions.ApiPushOnBehalfOf;
                case PackagePushVersion:
                    return ApiActions.ApiPushVersionOnBehalfOf;
                case PackageUnlist:
                    return ApiActions.ApiUnlistOnBehalfOf;
                case PackageVerify:
                    return ApiActions.ApiVerifyOnBehalfOf;
                default:
                    return PermissionLevel.Anonymous;
            }
        }
    }
}