// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGetGallery
{
    public class TyposquattingCheckResult
    {
        public bool WasUploadBlocked { get; }
        public IEnumerable<string> TyposquattingCheckCollisionIds { get; } // The return collision package Id list if it exists
        public IReadOnlyDictionary<TyposquattingCheckMetrics, object> TelemetryData { get; }

        public TyposquattingCheckResult(bool wasUploadBlocked, IEnumerable<string> typosquattingCheckCollisionIds,
                                         IDictionary<TyposquattingCheckMetrics, object> telemetryData)
        {
            WasUploadBlocked = wasUploadBlocked;
            TyposquattingCheckCollisionIds = typosquattingCheckCollisionIds;
            TelemetryData = new ReadOnlyDictionary<TyposquattingCheckMetrics, object>(telemetryData);
        }
    }
}
