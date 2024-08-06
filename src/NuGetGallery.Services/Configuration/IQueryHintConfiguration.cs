// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface IQueryHintConfiguration
    {
        /// <summary>
        /// Determines whether the RECOMPILE query hint should be used for the package dependents query. Some popular
        /// package IDs perform much better with their own dedicated query plan instead of OPTIMIZE FOR UNKNOWN.
        /// </summary>
        /// <param name="packageId">The package ID.</param>
        /// <returns>True, if the RECOMPILE query hint should be used for the provided package ID.</returns>
        bool ShouldUseRecompileForPackageDependents(string packageId);
    }
}