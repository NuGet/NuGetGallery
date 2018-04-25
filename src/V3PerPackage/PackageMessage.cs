// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.V3PerPackage
{
    public class PackageMessage
    {
        public const int Version = 1;

        [JsonConstructor]
        public PackageMessage(string packageId, string packageVersion)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
    }
}
