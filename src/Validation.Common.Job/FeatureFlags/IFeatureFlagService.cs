// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Determines whether validators should queue back to the orchestrator when their work is complete. The
        /// progress of this feature is tracked here:
        /// https://github.com/NuGet/NuGetGallery/issues/7185
        /// </summary>
        bool IsQueueBackEnabled();

        /// <summary>
        /// Determines whether orchestrator should try to acquire a lease specific to the entity it is processing.
        /// The progress of this work is tracked here:
        /// https://github.com/NuGet/NuGetGallery/issues/7629
        /// </summary>
        bool IsOrchestratorLeaseEnabled();

        /// <summary>
        /// Determines whether do extra Validation logging:
        /// https://github.com/NuGet/Engineering/issues/5250
        /// </summary>
        bool IsExtraValidationLoggingEnabled();
    }
}