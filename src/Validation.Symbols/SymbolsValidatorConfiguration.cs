// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Validation.Symbols
{
    public class SymbolsValidatorConfiguration
    {
        /// <summary>
        /// Connection string for the Azure storage the package validation container.
        /// </summary>
        public string ValidationPackageConnectionString { get; set; }

        /// <summary>
        /// Connection string for the Azure storage the packages container.
        /// </summary>
        public string PackageConnectionString { get; set; }

        /// <summary>
        /// Connection string for the Azure storage the Symbol validation container.
        /// </summary>
        public string ValidationSymbolsConnectionString { get; set; }
    }
}
