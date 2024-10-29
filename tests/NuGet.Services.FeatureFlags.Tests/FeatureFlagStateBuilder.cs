// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.FeatureFlags.Tests
{
    public class FeatureFlagStateBuilder
    {
        private readonly Dictionary<string, FeatureStatus> _features;
        private readonly Dictionary<string, Flight> _flights;

        public FeatureFlagStateBuilder()
        {
            _features = new Dictionary<string, FeatureStatus>();
            _flights = new Dictionary<string, Flight>();
        }

        public static FeatureFlagStateBuilder Create()
        {
            return new FeatureFlagStateBuilder();
        }

        public FeatureFlagStateBuilder WithFeature(string name, FeatureStatus status)
        {
            _features[name] = status;

            return this;
        }

        public FeatureFlagStateBuilder WithFlight(string name, Flight state)
        {
            _flights[name] = state;

            return this;
        }

        public FeatureFlags Build()
        {
            return new FeatureFlags(_features, _flights);
        }
    }
}
