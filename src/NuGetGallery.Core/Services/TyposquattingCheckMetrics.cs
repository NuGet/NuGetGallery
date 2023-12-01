// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// This enum is used to track the metrics for the typo-squatting check.
    /// </summary>
    public enum TyposquattingCheckMetrics
    {
        TrackMetricForTyposquattingChecklistRetrievalTime,
        TrackMetricForTyposquattingAlgorithmProcessingTime,
        TrackMetricForTyposquattingOwnersCheckTime,
        TrackMetricForTyposquattingCheckResultAndTotalTime
    }
}
