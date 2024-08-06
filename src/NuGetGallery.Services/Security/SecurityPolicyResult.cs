// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Result of a security policy evaluate action.
    /// </summary>
    public class SecurityPolicyResult
    {
        private readonly List<string> _warningMessages;
        public static SecurityPolicyResult SuccessResult = new SecurityPolicyResult(true, null, Array.Empty<string>());

        private SecurityPolicyResult(bool success, string errorMessage, string[] warningMessages)
        {
            Success = success;
            _warningMessages = warningMessages.ToList();
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Create a failed security policy result.
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static SecurityPolicyResult CreateErrorResult(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                throw new ArgumentNullException(nameof(errorMessage));
            }

            return new SecurityPolicyResult(false, errorMessage, Array.Empty<string>());
        }


        /// <summary>
        /// Create a warning package security policy result.
        /// </summary>
        /// <param name="warningMessages"></param>
        /// <returns></returns>
        public static SecurityPolicyResult CreateWarningResult(params string[] warningMessages)
        {
            if (warningMessages == null)
            {
                throw new ArgumentNullException(nameof(warningMessages));
            }

            return new SecurityPolicyResult(true, null, warningMessages);
        }

        /// <summary>
        /// Whether security policy criteria was successfully met.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Error message, if the security policy criteria was not met.
        /// </summary>
        public string ErrorMessage { get; }

        public bool HasWarnings => _warningMessages.Any();

        public IReadOnlyCollection<string> WarningMessages => _warningMessages;

        public void AddWarnings(IReadOnlyCollection<string> warningMessages)
        {
            _warningMessages.AddRange(warningMessages);
        }
    }
}