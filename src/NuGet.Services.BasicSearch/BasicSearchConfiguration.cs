// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using NuGet.Indexing;
using NuGet.Services.Configuration;

namespace NuGet.Services.BasicSearch
{
    /// <summary>
    /// Provides names of configuration settings for BasicSearch.
    /// </summary>
    public class BasicSearchConfiguration : IndexingConfiguration
    {
        private const string SearchPrefix = "Search.";
        private const string SerilogPrefix = "serilog:";

        [ConfigurationKeyPrefix(SerilogPrefix)]
        public string ApplicationInsightsInstrumentationKey { get; set; }

        [ConfigurationKeyPrefix(SearchPrefix)]
        [ConfigurationKey("IndexRefresh")]
        [DefaultValue(300)]
        public int IndexRefreshSec { get; set; }

        #region KeyVault
        private const string KeyVaultPrefix = "keyVault:";

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        public string VaultName { get; set; }

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        public string ClientId { get; set; }

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        [DefaultValue(StoreName.My)]
        public StoreName StoreName { get; set; }

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        [DefaultValue(StoreLocation.LocalMachine)]
        public StoreLocation StoreLocation { get; set; }

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        public string CertificateThumbprint { get; set; }

        [ConfigurationKeyPrefix(KeyVaultPrefix)]
        public bool ValidateCertificate { get; set; }
        #endregion
    }
}