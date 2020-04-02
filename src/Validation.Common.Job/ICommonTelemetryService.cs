// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation
{
    public interface ICommonTelemetryService
    {
        void TrackFileDownloaded(Uri fileUri, TimeSpan duration, long size);
    }
}