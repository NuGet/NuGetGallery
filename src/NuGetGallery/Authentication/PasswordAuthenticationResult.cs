// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public class PasswordAuthenticationResult
    {
        public enum AuthenticationResult
        {
            AccountLocked, // The account is locked
            BadCredentials, // Bad user name or password provided
            Success // All good
        }

        /// <summary>
        ///  The authentication status
        /// </summary>
        public AuthenticationResult Result { get; }

        /// <summary>
        /// If the account is locked, this is the period of time until unlock.
        /// </summary>
        public int LockTimeRemainingMinutes { get; }

        /// <summary>
        /// Is authentication was successful, this is the user details.
        /// </summary>
        public AuthenticatedUser AuthenticatedUser { get; }

        public PasswordAuthenticationResult(AuthenticationResult result, AuthenticatedUser authenticatedUser = null, int lockTimeRemainingMinutes = 0)
        {
            Result = result;
            LockTimeRemainingMinutes = lockTimeRemainingMinutes;
            AuthenticatedUser = authenticatedUser;
        }
    }
}