// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// Enriches logs with entry assembly NuGet metadata:
    /// AssemblyInformationalVersion
    /// Branch
    /// CommitId
    /// BuildDateUtc
    /// See PowerShell script that adds it: https://github.com/NuGet/ServerCommon/blob/a899cf3deba4c4d6a8194796e3a651a2ca520afe/build/common.ps1#L902-L908
    /// </summary>
    public class NuGetAssemblyMetadataEnricher : ILogEventEnricher
    {
        public const string PropertyName = "NuGetStartingAssemblyMetadata";
        private const string BranchMetadataKey = "Branch";
        private const string CommitIdMetadataKey = "CommitId";
        private const string BuildDateUtcMetadataKey = "BuildDateUtc";

        private LogEventProperty _cachedProperty = null;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_cachedProperty == null)
            {
                _cachedProperty = propertyFactory.CreateProperty(PropertyName, GetNuGetAssemblyMetadata(), destructureObjects: true);
            }
            logEvent.AddPropertyIfAbsent(_cachedProperty);
        }

        private static NuGetAssemblyMetadata GetNuGetAssemblyMetadata()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var attributes = entryAssembly.GetCustomAttributes();
            if (attributes == null)
            {
                return new NuGetAssemblyMetadata();
            }
            var metadata = new NuGetAssemblyMetadata();
            foreach (var attribute in attributes)
            {
                if (attribute is AssemblyInformationalVersionAttribute informationalVersion)
                {
                    metadata.AssemblyInformationalVersion = informationalVersion.InformationalVersion;
                }
                if (attribute is AssemblyMetadataAttribute metadataAttribute)
                {
                    switch (metadataAttribute.Key)
                    {
                        case BranchMetadataKey:
                            metadata.Branch = metadataAttribute.Value;
                            break;
                        case CommitIdMetadataKey:
                            metadata.CommitId = metadataAttribute.Value;
                            break;
                        case BuildDateUtcMetadataKey:
                            metadata.BuildDateUtc = metadataAttribute.Value;
                            break;
                    }
                }
            }
            return metadata;
        }

        private class NuGetAssemblyMetadata
        {
            public string AssemblyInformationalVersion { get; set; } = "<not specified>";
            public string Branch { get; set; } = null;
            public string CommitId { get; set; } = null;
            public string BuildDateUtc { get; set; } = null;
        }
    }
}
