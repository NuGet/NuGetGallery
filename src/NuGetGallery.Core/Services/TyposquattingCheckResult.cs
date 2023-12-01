// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGetGallery
{
    /// <summary>
    /// This class is used to return the result of the typo-squatting check and designed to keep not leak services such as telemetry to typosquatting service.
    /// </summary>
    public class TyposquattingCheckResult
    {
        public bool WasUploadBlocked { get; }
        public IEnumerable<string> TyposquattingCheckCollisionIds { get; } // The return collision package Id list if it exists
        public IReadOnlyDictionary<TyposquattingCheckMetrics, object> TelemetryData { get; } // Instead of emitting telemetry directly, we return the telemetry data to the caller, this way reduce dependency to typosquatting service.

        public TyposquattingCheckResult(bool wasUploadBlocked, IEnumerable<string> typosquattingCheckCollisionIds,
                                         IDictionary<TyposquattingCheckMetrics, object> telemetryData)
        {
            WasUploadBlocked = wasUploadBlocked;
            TyposquattingCheckCollisionIds = typosquattingCheckCollisionIds;
            TelemetryData = new ReadOnlyDictionary<TyposquattingCheckMetrics, object>(telemetryData);
        }
    }
}
