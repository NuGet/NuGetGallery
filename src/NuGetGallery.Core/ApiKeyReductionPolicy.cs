// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Shared constants and logic for the <c>ApiKeyReduction</c> feature. When the feature is enabled,
    /// API keys whose total duration is longer than <see cref="DurationThresholdDays"/> days are treated
    /// as expiring no later than <see cref="CutoffUtc"/>. This type is referenced by the gallery
    /// (push enforcement and API key list display) and by the credential expiration email job so the
    /// feature flag name, cutoff date, and duration threshold never diverge.
    /// </summary>
    public static class ApiKeyReductionPolicy
    {
        /// <summary>
        /// The feature flag name evaluated by both the gallery and the credential expiration job.
        /// </summary>
        public const string FeatureName = "NuGetGallery.ApiKeyReduction";

        /// <summary>
        /// The hardcoded cutoff date. API keys with a duration longer than <see cref="DurationThresholdDays"/>
        /// days may not effectively expire later than this date.
        /// </summary>
        public static readonly DateTime CutoffUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The duration threshold, in days, above which the cutoff applies.
        /// This is the single source of truth: it is consumed by the gallery's
        /// <see cref="GetEffectiveExpiration"/> and flows to the Gallery.CredentialExpiration job's
        /// GetExpiredCredentialsQuery as the @DurationThresholdDays parameter.
        /// </summary>
        public const int DurationThresholdDays = 30;

        /// <summary>
        /// Computes the effective expiration for an API key credential. When the credential's duration
        /// (<paramref name="expires"/> minus <paramref name="created"/>) is longer than
        /// <see cref="DurationThresholdDays"/> days and its expiration is after <see cref="CutoffUtc"/>,
        /// the effective expiration is capped to <see cref="CutoffUtc"/>. Otherwise the original
        /// expiration is returned unchanged.
        /// </summary>
        public static DateTime? GetEffectiveExpiration(DateTime created, DateTime? expires)
        {
            if (expires.HasValue
                && (expires.Value - created).TotalDays > DurationThresholdDays
                && expires.Value > CutoffUtc)
            {
                return CutoffUtc;
            }

            return expires;
        }
    }
}
