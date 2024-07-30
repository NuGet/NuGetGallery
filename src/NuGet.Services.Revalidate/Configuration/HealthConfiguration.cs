// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    public class HealthConfiguration
    {
        /// <summary>
        /// The name of the Azure Blob Storage container that stores status information.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The name of the Azure Blob Storage blob that stores status information.
        /// </summary>
        public string StatusBlobName { get; set; }

        /// <summary>
        /// The path to the component that the revalidation job will monitor. The revalidation job will
        /// pause if this component isn't healthy.
        /// </summary>
        public string ComponentPath { get; set; }
    }
}
