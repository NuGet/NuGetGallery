// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationRootSecretReaderFactoryFacts
    {
        [Fact]
        public void ConstructorThrowsWhenKeyConfigIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationRootSecretReaderFactory(null));
        }

        public static IEnumerable<object[]> InvalidConfigs
        {
            get
            {
                yield return new object[] {
                    new Dictionary<string, string> {
                        { Constants.KeyVaultVaultNameKey, "KeyVaultName" },
                        { Constants.KeyVaultUseManagedIdentity, "true" },
                        { Constants.KeyVaultClientIdKey, "KeyVaultClientId" },
                        { Constants.KeyVaultCertificateThumbprintKey, "KeyVaultThumbprint" },
                        { Constants.KeyVaultStoreNameKey, "StoreName"},
                        { Constants.KeyVaultStoreLocationKey, "StoreLocation" },
                        { Constants.KeyVaultValidateCertificateKey, "true" },
                        { Constants.KeyVaultSendX5c, "false" }
                    }
                };
            }
        }

        public static IEnumerable<object[]> ValidConfigs
        {
            get
            {
                yield return new object[] {
                    new Dictionary<string, string> {
                        { Constants.KeyVaultVaultNameKey, "KeyVaultName" },
                        { Constants.KeyVaultUseManagedIdentity, "true" },
                    }
                };

                yield return new object[] {
                    new Dictionary<string, string> {
                        { Constants.KeyVaultClientIdKey, "KeyVaultClientId" },
                        { Constants.KeyVaultCertificateThumbprintKey, "KeyVaultThumbprint" },
                        { Constants.KeyVaultStoreNameKey, "StoreName"},
                        { Constants.KeyVaultStoreLocationKey, "StoreLocation" },
                        { Constants.KeyVaultValidateCertificateKey, "true" },
                        { Constants.KeyVaultSendX5c, "false" }
                    }
                };

                yield return new object[] {
                    new Dictionary<string, string> {
                        { Constants.KeyVaultVaultNameKey, "KeyVaultName" },
                        { Constants.KeyVaultUseManagedIdentity, "false" },
                        { Constants.KeyVaultClientIdKey, "KeyVaultClientId" },
                        { Constants.KeyVaultCertificateThumbprintKey, "KeyVaultThumbprint" },
                        { Constants.KeyVaultStoreNameKey, "StoreName"},
                        { Constants.KeyVaultStoreLocationKey, "StoreLocation" },
                        { Constants.KeyVaultValidateCertificateKey, "true" },
                        { Constants.KeyVaultSendX5c, "false" }
                    }
                };

                yield return new object[] {
                    new Dictionary<string, string> {
                        { Constants.KeyVaultVaultNameKey, "KeyVaultName" },
                        { Constants.KeyVaultUseManagedIdentity, "false" },
                        { Constants.KeyVaultClientIdKey, "" },
                        { Constants.KeyVaultCertificateThumbprintKey, "" },
                        { Constants.KeyVaultStoreNameKey, ""},
                        { Constants.KeyVaultStoreLocationKey, "" },
                        { Constants.KeyVaultValidateCertificateKey, "" },
                        { Constants.KeyVaultSendX5c, "" }
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidConfigs))]
        public void ConstructorThrowsWhenKeyVaultConfigSpecifiesManagedIdentityAndCertificate(IDictionary<string, string> config)
        {
            Assert.Throws<ArgumentException>(() => new ConfigurationRootSecretReaderFactory(CreateTestConfiguration(config)));
        }

        [Theory]
        [MemberData(nameof(ValidConfigs))]
        public void CreatesSecretReaderFactoryForValidConfiguration(IDictionary<string, string> config)
        {
            var secretReaderFacotry = new ConfigurationRootSecretReaderFactory(CreateTestConfiguration(config));
            Assert.NotNull(secretReaderFacotry);
        }


        private IConfigurationRoot CreateTestConfiguration(IDictionary<string, string> config)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(config)
                .Build();
        }
    }
}
