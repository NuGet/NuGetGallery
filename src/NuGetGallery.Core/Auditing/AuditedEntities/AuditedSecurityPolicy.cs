// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedSecurityPolicy
    {
        public string Type { get; set; }

        public string Value { get; set; }

        public static AuditedSecurityPolicy CreateFrom(SecurityPolicy securityPolicy)
        {
            return new AuditedSecurityPolicy
            {
                Type = securityPolicy.GetType().Name,
                Value = securityPolicy.Value
            };
        }
    }
}
