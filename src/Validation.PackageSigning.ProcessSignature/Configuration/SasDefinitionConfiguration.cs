// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.PackageSigning.Configuration
{
    public class SasDefinitionConfiguration
    {
        /// <summary>
        /// Sas definition name already stored on key vault that is used by the <see cref="SignatureValidator"/>.
        /// </summary>
        public string SignatureValidatorSasDefinition { get; set; }
    }
}
