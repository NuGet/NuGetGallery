// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.Telemetry
{
    public interface ITelemetryService
    {
        void TrackDurationToStripRepositorySignatures(
            TimeSpan duration,
            string packageId,
            string normalizedVersion,
            Guid validationId,
            bool changed);

        void TrackStrippedRepositorySignatures(
            string packageId,
            string normalizedVersion,
            Guid validationId,
            PrimarySignature inputSignature,
            PrimarySignature outputSignature);
    }
}