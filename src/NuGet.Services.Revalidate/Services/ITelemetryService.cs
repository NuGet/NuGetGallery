// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    public interface ITelemetryService
    {
        IDisposable TrackDurationToStartNextRevalidation();

        void TrackPackageRevalidationMarkedAsCompleted(string packageId, string normalizedVersion);

        void TrackPackageRevalidationStarted(string packageId, string normalizedVersion);
    }
}
