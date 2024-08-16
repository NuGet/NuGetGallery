// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    public enum SymbolsPackageIngestRequestStatus
    {
        /// <summary>
        /// The symbols package was ingested.
        /// </summary>
        Ingested = 0,

        /// <summary>
        /// The symbols package is in the process to be ingested.
        /// </summary>
        Ingesting = 1,

        /// <summary>
        /// The symbols package failed to be ingested. 
        /// </summary>
        FailedIngestion = 2,
    }
}
