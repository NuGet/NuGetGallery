// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.Services.Security
{
    public abstract class SecurityPolicyHandler<TContext>
    {
        public string Name { get; }

        public SecurityPolicyAction Action { get; }

        public SecurityPolicyHandler(string name, SecurityPolicyAction action)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Action = action;
        }

        public abstract Task<SecurityPolicyResult> EvaluateAsync(TContext context);
    }
}