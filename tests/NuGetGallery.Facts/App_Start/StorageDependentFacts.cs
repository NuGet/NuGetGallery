// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class StorageDependentFacts
    {
        [Fact]
        public void AllTypesDependingOnIFileStorageServiceAreReturnedByGetStorageDependents()
        {
            // Arrange
            var config = GetConfiguration();

            // Act
            var dependents = StorageDependent.GetAll(config);

            // Assert
            var actual = new HashSet<Type>(dependents.Select(x => x.ImplementationType));
            var allTypes = typeof(DefaultDependenciesModule).Assembly.GetTypes();
            var expected = new HashSet<Type>();
            foreach (var type in allTypes)
            {
                var constructors = type.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    if (parameters.Any(p => p.ParameterType == typeof(IFileStorageService)))
                    {
                        expected.Add(type);
                    }
                }
            }
            
            Assert.Subset(expected, actual);
            Assert.Subset(actual, expected);
        }

        [Fact]
        public void StorageDependentsHaveExpectedTypes()
        {
            // Arrange
            var config = GetConfiguration();

            // Act
            var dependents = StorageDependent.GetAll(config);

            // Assert
            var implementationToInterface = dependents.ToDictionary(x => x.ImplementationType, x => x.InterfaceType);
            Assert.Contains(typeof(CertificateService), implementationToInterface.Keys);
            Assert.Contains(typeof(ContentService), implementationToInterface.Keys);
            Assert.Contains(typeof(PackageFileService), implementationToInterface.Keys);
            Assert.Contains(typeof(SymbolPackageFileService), implementationToInterface.Keys);
            Assert.Contains(typeof(UploadFileService), implementationToInterface.Keys);
            Assert.Equal(5, implementationToInterface.Count);
            Assert.Equal(typeof(ICertificateService), implementationToInterface[typeof(CertificateService)]);
            Assert.Equal(typeof(IContentService), implementationToInterface[typeof(ContentService)]);
            Assert.Equal(typeof(IPackageFileService), implementationToInterface[typeof(PackageFileService)]);
            Assert.Equal(typeof(ISymbolPackageFileService), implementationToInterface[typeof(SymbolPackageFileService)]);
            Assert.Equal(typeof(IUploadFileService), implementationToInterface[typeof(UploadFileService)]);
        }

        [Fact]
        public void StorageDependentsUseCorrectConnectionString()
        {
            // Arrange
            var config = GetConfiguration();

            // Act
            var dependents = StorageDependent.GetAll(config);

            // Assert
            var typeToConnectionString = dependents.ToDictionary(x => x.ImplementationType, x => x.AzureStorageConnectionString);
            Assert.Equal(typeToConnectionString[typeof(CertificateService)], config.AzureStorage_UserCertificates_ConnectionString);
            Assert.Equal(typeToConnectionString[typeof(ContentService)], config.AzureStorage_Content_ConnectionString);
            Assert.Equal(typeToConnectionString[typeof(PackageFileService)], config.AzureStorage_Packages_ConnectionString);
            Assert.Equal(typeToConnectionString[typeof(UploadFileService)], config.AzureStorage_Uploads_ConnectionString);
        }

        [Fact]
        public void StorageDependentsAreGroupedByConnectionString()
        {
            // Arrange
            var mock = new Mock<IAppConfiguration>();
            mock.Setup(x => x.AzureStorage_UserCertificates_ConnectionString).Returns("Certificates");
            mock.Setup(x => x.AzureStorage_Content_ConnectionString).Returns("Content");
            mock.Setup(x => x.AzureStorage_Packages_ConnectionString).Returns("Packages and Uploads");
            mock.Setup(x => x.AzureStorage_Uploads_ConnectionString).Returns("Packages and Uploads");
            var config = mock.Object;

            // Act
            var dependents = StorageDependent.GetAll(config);

            // Assert
            var typeToBindingKey = dependents.ToDictionary(x => x.ImplementationType, x => x.BindingKey);
            Assert.Equal(typeToBindingKey[typeof(PackageFileService)], typeToBindingKey[typeof(UploadFileService)]);
        }

        private static IAppConfiguration GetConfiguration()
        {
            var mock = new Mock<IAppConfiguration>();
            mock.Setup(x => x.AzureStorage_UserCertificates_ConnectionString).Returns("Certificates");
            mock.Setup(x => x.AzureStorage_Content_ConnectionString).Returns("Content");
            mock.Setup(x => x.AzureStorage_Packages_ConnectionString).Returns("Packages");
            mock.Setup(x => x.AzureStorage_Uploads_ConnectionString).Returns("Uploads");
            return mock.Object;
        }
    }
}
