// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;

#nullable enable

namespace NuGet.Services.Logging
{
    public class NuGetAssemblyMetadata
    {
        private const string BranchMetadataKey = "Branch";
        private const string CommitIdMetadataKey = "CommitId";
        private const string BuildDateUtcMetadataKey = "BuildDateUtc";

        public string? AssemblyInformationalVersion { get; set; } = null;
        public string? Branch { get; set; } = null;
        public string? CommitId { get; set; } = null;
        public string? BuildDateUtc { get; set; } = null;

        public NuGetAssemblyMetadata(Assembly metadataSourceAssembly)
        {
            var attributes = metadataSourceAssembly?.GetCustomAttributes();
            if (attributes is null)
            {
                return;
            }
            foreach (var attribute in attributes)
            {
                if (attribute is AssemblyInformationalVersionAttribute informationalVersion)
                {
                    AssemblyInformationalVersion = informationalVersion.InformationalVersion;
                }
                if (attribute is AssemblyMetadataAttribute metadataAttribute)
                {
                    switch (metadataAttribute.Key)
                    {
                        case BranchMetadataKey:
                            Branch = metadataAttribute.Value;
                            break;
                        case CommitIdMetadataKey:
                            CommitId = metadataAttribute.Value;
                            break;
                        case BuildDateUtcMetadataKey:
                            BuildDateUtc = metadataAttribute.Value;
                            break;
                    }
                }
            }
        }
    }
}
