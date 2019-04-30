// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Auditing.AuditedEntities;
using NuGetGallery.Features;

namespace NuGetGallery.Auditing
{
    public sealed class FeatureFlagsAuditRecord : AuditRecord<AuditedFeatureFlagsAction>
    {
        public AuditedFeatureFlagFeature[] Features { get; }
        public AuditedFeatureFlagFlight[] Flights { get; }
        public string ContentId { get; }
        public FeatureFlagSaveResult Result { get; }

        public FeatureFlagsAuditRecord(
            AuditedFeatureFlagsAction action, 
            FeatureFlags flags,
            string contentId, 
            FeatureFlagSaveResult result)
            : base(action)
        {
            if (flags == null)
            {
                throw new ArgumentNullException(nameof(flags));
            }

            ContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));

            Features = AuditedFeatureFlagFeature.CreateFrom(flags);
            Flights = AuditedFeatureFlagFlight.CreateFrom(flags);
            
            Result = result;

            // Group feature flags changes by the month they occurred in.
            var currentTime = DateTime.UtcNow;
            _path = $"{currentTime.Year}-{currentTime.Month}";
        }

        private readonly string _path;
        public override string GetPath()
        {
            return _path;
        }
    }
}