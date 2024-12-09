// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

#nullable enable

namespace NuGet.Services.Logging
{
    public class NuGetAssemblyMetadataTelemetryInitializer : ITelemetryInitializer
    {
        private readonly NuGetAssemblyMetadata _assemblyMetadata;

        public NuGetAssemblyMetadataTelemetryInitializer(Assembly metadataSourceAssembly)
        {
            if (metadataSourceAssembly is null)
            {
                throw new ArgumentNullException(nameof(metadataSourceAssembly));
            }

            _assemblyMetadata = new NuGetAssemblyMetadata(metadataSourceAssembly);
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is not ISupportProperties itemTelemetry)
            {
                return;
            }

            itemTelemetry.Properties
                .AddPropertyIfAbsent(nameof(_assemblyMetadata.AssemblyInformationalVersion), _assemblyMetadata.AssemblyInformationalVersion)
                .AddPropertyIfAbsent(nameof(_assemblyMetadata.Branch), _assemblyMetadata.Branch)
                .AddPropertyIfAbsent(nameof(_assemblyMetadata.CommitId), _assemblyMetadata.CommitId)
                .AddPropertyIfAbsent(nameof(_assemblyMetadata.BuildDateUtc), _assemblyMetadata.BuildDateUtc);
        }
    }

    internal static class DictionaryExtensions
    {
        public static IDictionary<string, string> AddPropertyIfAbsent(
            this IDictionary<string, string> dictionary,
            string propertyName,
            string? propertyValue)
        {
            if (!dictionary.ContainsKey(propertyName) && propertyValue is not null)
            {
                dictionary.Add(propertyName, propertyValue);
            }

            return dictionary;
        }
    }
}
