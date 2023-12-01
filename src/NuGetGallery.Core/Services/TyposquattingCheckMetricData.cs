// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// Used for TyposquattingCheck service return metric without adding a dependency on the telemetry service.
    /// </summary>
    public class TyposquattingCheckMetricData
    {
        public TimeSpan TotalTime { get; set; }
        public bool WasUploadBlocked { get; set; }
        public IReadOnlyCollection<string> TyposquattingCheckCollisionIds { get; set; }
        public int PackageIdsCheckListCount { get; set; }
        public TimeSpan CheckListExpireTimeInHours { get; set; }
    }
}
