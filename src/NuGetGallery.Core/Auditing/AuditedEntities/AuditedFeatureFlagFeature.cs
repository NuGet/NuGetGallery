// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedFeatureFlagFeature
    {
        public string Name { get; private set; }
        public FeatureStatus Status { get; private set; }

        public static AuditedFeatureFlagFeature[] CreateFrom(FeatureFlags flags)
        {
            return flags.Features?
                .Select(f => CreateFrom(f.Key, f.Value))
                .ToArray() ?? Array.Empty<AuditedFeatureFlagFeature>();
        }

        public static AuditedFeatureFlagFeature CreateFrom(string name, FeatureStatus status)
        {
            return new AuditedFeatureFlagFeature
            {
                Name = name,
                Status = status
            };
        }
    }
}
