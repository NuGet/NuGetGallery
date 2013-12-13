using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.ServiceModel;
using Xunit;

namespace NuGet.Services.Configuration
{
    public class StorageConfigurationFacts
    {
        // AccountKey in the test data is just a random key I generated to have the same length/format

        [Fact]
        public void LoadsAllKnownStorageAccounts()
        {
            // Arrange
            var host = new Mock<ServiceHost>();
            host.Setup(h => h.GetConfigurationSetting("Storage.Primary")).Returns("AccountName=primary;AccountKey=WT76mUSw8V1epiptlpX8ZtA1udGuXSh1/Z5nBi5MgZWQmYSPp3DMs5S1nnoBIl1ny7KU4Pi8Gum8wffNsBtssA==;DefaultEndpointsProtocol=https");
            host.Setup(h => h.GetConfigurationSetting("Storage.Backup")).Returns("AccountName=backup;AccountKey=WT76mUSw8V1epiptlpX8ZtA1udGuXSh1/Z5nBi5MgZWQmYSPp3DMs5S1nnoBIl1ny7KU4Pi8Gum8wffNsBtssA==;DefaultEndpointsProtocol=https");
            var hub = new ConfigurationHub(host.Object);

            // Act
            var actual = hub.GetSection<StorageConfiguration>();

            // Assert
            Assert.Equal("primary", actual.GetAccount(KnownStorageAccount.Primary).Credentials.AccountName);
            Assert.Equal("backup", actual.GetAccount(KnownStorageAccount.Backup).Credentials.AccountName);
        }

        [Fact]
        public void IgnoresMissingAccounts()
        {
            // Arrange
            var host = new Mock<ServiceHost>();
            host.Setup(h => h.GetConfigurationSetting("Storage.Primary")).Returns("AccountName=primary;AccountKey=WT76mUSw8V1epiptlpX8ZtA1udGuXSh1/Z5nBi5MgZWQmYSPp3DMs5S1nnoBIl1ny7KU4Pi8Gum8wffNsBtssA==;DefaultEndpointsProtocol=https");
            var hub = new ConfigurationHub(host.Object);

            // Act
            var actual = hub.GetSection<StorageConfiguration>();

            // Assert
            Assert.Equal("primary", actual.GetAccount(KnownStorageAccount.Primary).Credentials.AccountName);
            Assert.Null(actual.GetAccount(KnownStorageAccount.Backup));
        }

        [Fact]
        public void IgnoresUnknownAccounts()
        {
            // Arrange
            var host = new Mock<ServiceHost>();
            host.Setup(h => h.GetConfigurationSetting("Storage.Woozle")).Returns("AccountName=primary;AccountKey=WT76mUSw8V1epiptlpX8ZtA1udGuXSh1/Z5nBi5MgZWQmYSPp3DMs5S1nnoBIl1ny7KU4Pi8Gum8wffNsBtssA==;DefaultEndpointsProtocol=https");
            var hub = new ConfigurationHub(host.Object);

            // Act
            var actual = hub.GetSection<StorageConfiguration>();

            // Assert
            Assert.Null(actual.GetAccount(KnownStorageAccount.Primary));
            Assert.Null(actual.GetAccount(KnownStorageAccount.Backup));
        }
    }
}
