// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Services.Revalidate.Tests.TestData
{
    internal class TestResources
    {
        private const string ResourceNamespace = "NuGet.Services.Revalidate.Tests.TestData";

        public const string PackagePublishingDegradedStatus = ResourceNamespace + ".PackagePublishingDegradedStatus.json";
        public const string PackagePublishingDownStatus = ResourceNamespace + ".PackagePublishingDownStatus.json";
        public const string PackagePublishingUpStatus = ResourceNamespace + ".PackagePublishingUpStatus.json";

        public const string PackagePublishingMissingStatus = ResourceNamespace + ".PackagePublishingMissingStatus.json";

        /// <summary>
        /// Buffer the resource stream into memory so the caller doesn't have to dispose.
        /// </summary>
        public static MemoryStream GetResourceStream(string resourceName)
        {
            var resourceStream = typeof(TestResources)
                .Assembly
                .GetManifestResourceStream(resourceName);

            if (resourceStream == null)
            {
                return null;
            }

            var bufferedStream = new MemoryStream();
            using (resourceStream)
            {
                resourceStream.CopyTo(bufferedStream);
            }

            bufferedStream.Position = 0;
            return bufferedStream;
        }
    }
}
