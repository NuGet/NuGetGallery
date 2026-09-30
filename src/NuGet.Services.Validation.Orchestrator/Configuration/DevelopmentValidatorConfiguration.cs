// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    public class DevelopmentValidatorConfiguration
    {
        public bool Enabled { get; set; }

        /// <summary>
        /// Uses the development validator instead of malware scanning for local symbol validation.
        /// Requires <see cref="Enabled"/>; leave disabled outside local development.
        /// </summary>
        public bool UseForSymbolScan { get; set; }

        /// <summary>
        /// Simulates symbol ingestion for local promotion instead of uploading to the symbol server.
        /// Requires <see cref="Enabled"/>; leave disabled outside local development.
        /// </summary>
        public bool UseForSymbolsIngester { get; set; }

        public int DelaySeconds { get; set; }

        public string FailurePackageIdPrefix { get; set; }
    }
}
