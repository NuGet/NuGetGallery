// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class ApiActions
    {
        /// <summary>
        /// The user specified by an API key's owner scope can perform all API actions on packages.
        /// </summary>
        public static PermissionLevel ApiAll = ApiPush & ApiUnlist & ApiVerify;

        /// <summary>
        /// The user can perform all API actions on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiAllOnBehalfOf =
            ApiPushOnBehalfOf &
            ApiPushVersionOnBehalfOf &
            ApiUnlistOnBehalfOf &
            ApiVerifyOnBehalfOf;

        /// <summary>
        /// The user can perform at least one API action on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiAnyOnBehalfOf =
            ApiPushOnBehalfOf |
            ApiPushVersionOnBehalfOf |
            ApiUnlistOnBehalfOf |
            ApiVerifyOnBehalfOf;

        /// <summary>
        /// The user specified by an API key's owner scope can push new versions an existing package using the API.
        /// </summary>
        public static PermissionLevel ApiPush = PermissionLevel.Owner;

        /// <summary>
        /// The user can push new package IDs on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiPushOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        /// <summary>
        /// The user can push new versions of an existing package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiPushVersionOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user specified by an API key's owner scope can unlist and relist existing versions of the package using the API.
        /// </summary>
        public static PermissionLevel ApiUnlist = PermissionLevel.Owner;

        /// <summary>
        /// The user can unlist and relist existing versions of a package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiUnlistOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user specified by an API key's owner scope can create a verification key for a package using the API.
        /// </summary>
        public static PermissionLevel ApiVerify = PermissionLevel.Owner;

        /// <summary>
        /// The user can create a verification key for a package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiVerifyOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;
    }
}