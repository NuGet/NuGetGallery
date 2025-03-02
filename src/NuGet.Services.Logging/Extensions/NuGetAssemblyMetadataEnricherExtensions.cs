// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Serilog;
using Serilog.Configuration;
using Serilog.Enrichers;

namespace NuGet.Services.Logging
{
    public static class NuGetAssemblyMetadataEnricherExtensions
    {
        public static LoggerConfiguration WithNuGetAssemblyMetadata(
            this LoggerEnrichmentConfiguration enrichmentConfiguration,
            Assembly metadataSourceAssembly)
        {
            if (enrichmentConfiguration == null)
            {
                throw new ArgumentNullException(nameof(enrichmentConfiguration));
            }

            if (metadataSourceAssembly == null)
            {
                throw new ArgumentNullException(nameof(metadataSourceAssembly));
            }

            return enrichmentConfiguration.With(new NuGetAssemblyMetadataEnricher(metadataSourceAssembly));
        }
    }
}
