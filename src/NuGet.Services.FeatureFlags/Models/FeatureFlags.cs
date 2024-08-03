// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.FeatureFlags
{
    /// <summary>
    /// The state of all features and flights.
    /// </summary>
    public class FeatureFlags
    {
        public FeatureFlags(
            IReadOnlyDictionary<string, FeatureStatus> features,
            IReadOnlyDictionary<string, Flight> flights)
        {
            Features = ToCaseInsensitive(features);
            Flights = ToCaseInsensitive(flights);

            if (Flights.Values.Any(f => f == null))
            {
                throw new ArgumentException("Flights cannot be null", nameof(flights));
            }
        }

        private static IReadOnlyDictionary<string, T> ToCaseInsensitive<T>(IReadOnlyDictionary<string, T> input)
        {
            if (input == null)
            {
                return new Dictionary<string, T>();
            }

            var dictionary = input.ToDictionary(g => g.Key, g => g.Value);

            return new Dictionary<string, T>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The state of features. Keys are features' unique identifiers and case insensitive.
        /// Values are features' statuses (either enabled or disabled).
        /// </summary>
        public IReadOnlyDictionary<string, FeatureStatus> Features { get; }

        /// <summary>
        /// The state of feature flights. Unlike features, flights can have enabled partially
        /// for specific users. Keys are flights' unique identifiers and case insensitive.
        /// Values are flights status.
        /// </summary>
        public IReadOnlyDictionary<string, Flight> Flights { get; }
    }
}
