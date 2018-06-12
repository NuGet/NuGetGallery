// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Revalidate
{
    public class InitializationConfiguration
    {
        /// <summary>
        /// The list of filesystem paths that contain packages that are preinstalled by Visual Studio and the
        /// .NET SDK. Environment variables will be expanded before evaluation.
        /// </summary>
        public List<string> PreinstalledPaths { get; set; }

        /// <summary>
        /// The revalidation job should not revalidate packages that were uploaded after repository signing was
        /// enabled. Packages created on or after this value will not be revalidated.
        /// </summary>
        public DateTimeOffset MaxPackageCreationDate { get; set; }

        /// <summary>
        /// The time to sleep between initialization batches to prevent overloading databases.
        /// </summary>
        public TimeSpan SleepDurationBetweenBatches { get; set; }
    }
}
