// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class SecurityPolicyResult
    {
        public SecurityPolicy Policy { get; }

        public string ErrorMessage { get; }

        public bool HasError
        {
            get
            {
                return !string.IsNullOrEmpty(ErrorMessage);
            }
        }

        public SecurityPolicyResult(SecurityPolicy policy = null, string errorMessage = null)
        {
            Policy = policy;
            ErrorMessage = errorMessage;
        }
    }
}