// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Auditing.AuditedEntities
{
    /// <summary>
    /// Auditing details for UserSecurityPolicy entity.
    /// </summary>
    public class AuditedUserSecurityPolicy
    {
        public string Name { get; }
        public string Subscription { get; }
        public string Value { get; }

        public AuditedUserSecurityPolicy(UserSecurityPolicy policy)
        {
            Name = policy.Name;
            Subscription = policy.Subscription;
            Value = policy.Value;
        }
    }
}