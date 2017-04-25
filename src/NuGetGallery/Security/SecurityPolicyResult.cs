// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Security
{
    /// <summary>
    /// Result of a security policy evaluate action.
    /// </summary>
    public class SecurityPolicyResult
    {
        public static SecurityPolicyResult SuccessResult = new SecurityPolicyResult(true, null);

        public SecurityPolicyResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Whether security policy criteria was successfully met.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Error message, if the security policy criteria was not met.
        /// </summary>
        public string ErrorMessage { get; private set; }
    }
}