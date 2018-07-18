// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public interface ITelemetryService
    {
        void TrackHealthyInstanceCount(string region, int count);
        void TrackInstanceCount(string region, int count);
        void TrackInstanceReboot(string region, int index);
        void TrackInstanceRebootDuration(string region, int index, TimeSpan duration, InstanceHealth health);
        void TrackUnhealthyInstanceCount(string region, int count);
        void TrackUnknownInstanceCount(string region, int count);
    }
}