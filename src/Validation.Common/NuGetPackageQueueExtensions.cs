// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Jobs.Validation.Common
{
    public static class NuGetPackageQueueExtensions
    {
        private const string Truncated = "(truncated)";

        /// <summary>
        /// Azure Queues have a max message length of 65536 bytes.
        /// This method truncates potentially long fields so that a serialized representation
        /// of the package falls within that boundary.
        /// </summary>
        /// <param name="package">The package to truncate</param>
        /// <returns>Truncated package</returns>
        public static NuGetPackage TruncateForAzureQueue(this NuGetPackage package)
        {
            // Clone the package
            var clone = JsonConvert.DeserializeObject<NuGetPackage>(
                JsonConvert.SerializeObject(package));

            // Truncate long properties (https://github.com/NuGet/NuGet.Jobs/pull/54/files/228105a40129c076afc9b9e21551ffadef315f92#r70679869)
            clone.Description = Truncated;
            clone.ReleaseNotes = Truncated;
            clone.Summary = Truncated;
            clone.Tags = Truncated;

            return clone;
        }
    }
}