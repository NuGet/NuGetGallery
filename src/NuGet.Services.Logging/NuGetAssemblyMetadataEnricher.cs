// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

#nullable enable

namespace NuGet.Services.Logging
{
    /// <summary>
    /// Enriches logs with entry assembly NuGet metadata:
    /// AssemblyInformationalVersion
    /// Branch
    /// CommitId
    /// BuildDateUtc
    /// See PowerShell script that adds it: https://github.com/NuGet/NuGetGallery/blob/27bc6a62ead3110c1d8d2d2794fc8b8b646b1f1d/build/common.ps1#L884-L889
    /// </summary>
    public class NuGetAssemblyMetadataEnricher : ILogEventEnricher
    {
        public const string PropertyName = "NuGetStartingAssemblyMetadata";

        private LogEventProperty? _cachedProperty = null;
        private readonly bool _hasAssemblyMetadata = false;
        private readonly NuGetAssemblyMetadata _assemblyMetadata;

        public NuGetAssemblyMetadataEnricher(Assembly metadataSourceAssembly)
        {
            if (metadataSourceAssembly == null)
            {
                throw new ArgumentNullException(nameof(metadataSourceAssembly));
            }

            _assemblyMetadata = new NuGetAssemblyMetadata(metadataSourceAssembly);
            _hasAssemblyMetadata =
                _assemblyMetadata.AssemblyInformationalVersion != null
                || _assemblyMetadata.Branch != null
                || _assemblyMetadata.CommitId != null
                || _assemblyMetadata.BuildDateUtc != null;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!_hasAssemblyMetadata)
            {
                return;
            }

            _cachedProperty ??= propertyFactory.CreateProperty(PropertyName, _assemblyMetadata, destructureObjects: true);

            logEvent.AddPropertyIfAbsent(_cachedProperty);
        }
    }
}
