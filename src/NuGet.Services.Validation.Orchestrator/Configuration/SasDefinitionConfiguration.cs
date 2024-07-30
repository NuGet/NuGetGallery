// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    public class SasDefinitionConfiguration
    {
        /// <summary>
        /// Sas definition name already stored on key vault that is used by the <see cref="PackageStatusProcessor"/>.
        /// </summary>
        public string PackageStatusProcessorSasDefinition { get; set; }

        /// <summary>
        /// Sas definition name already stored on key vault that is used by the <see cref="ValidationSetProvider{T}"/>.
        /// </summary>
        public string ValidationSetProviderSasDefinition { get; set; }

        /// <summary>
        /// Sas definition name already stored on key vault that is used by the <see cref="ValidationSetProcessor"/>.
        /// </summary>
        public string ValidationSetProcessorSasDefinition { get; set; }
    }
}
