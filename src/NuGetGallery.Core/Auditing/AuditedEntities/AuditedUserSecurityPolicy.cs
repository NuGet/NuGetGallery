// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Auditing.AuditedEntities
{
    /// <summary>
    /// Auditing details for UserSecurityPolicy entity.
    /// </summary>
    public class AuditedUserSecurityPolicy(UserSecurityPolicy policy)
    {
        public string Name { get; } = policy.Name;
        public string Subscription { get; } = policy.Subscription;
        public string Value { get; } = policy.Value;
    }
}