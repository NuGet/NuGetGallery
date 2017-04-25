// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Filters;

namespace NuGetGallery.Security
{
    /// <summary>
    /// Policy handler that defines behavior for specific user policy types.
    /// </summary>
    public abstract class UserSecurityPolicyHandler
    {
        public string Name { get; }

        public SecurityPolicyAction Action { get; }

        public UserSecurityPolicyHandler(string name, SecurityPolicyAction action)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Action = action;
        }

        public abstract SecurityPolicyResult Evaluate(UserSecurityPolicyContext context);
    }
}