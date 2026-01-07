// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Responsible for initializing, serializing, and deserializing <see cref="ABTestEnrollment"/> instances.
    /// </summary>
    public interface IABTestEnrollmentFactory
    {
        /// <summary>
        /// Initialize the A/B test enrollment instance. This is where the "coin is flipped" regarding which variants
        /// of a behavior a user will experience.
        /// </summary>
        ABTestEnrollment Initialize();

        /// <summary>
        /// Serialize an existing A/B test enrollment to a string.
        /// </summary>
        string Serialize(ABTestEnrollment enrollment);

        /// <summary>
        /// Try to deserialize an A/B test enrollment from a string. <paramref name="enrollment"/> will be set to null
        /// if the return value is false. It will be set to a non-null enrollment instance if the return value is true.
        /// </summary>
        /// <param name="serialized">The serialized A/B test enrollment.</param>
        /// <param name="enrollment">The output enrollment.</param>
        /// <returns>True if the enrollment was deserialized. False otherwise.</returns>
        bool TryDeserialize(string serialized, out ABTestEnrollment enrollment);
    }
}