// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.V3
{
    public interface IV3TelemetryService
    {
        IDisposable TrackCatalogLeafDownloadBatch(int count);
    }
}