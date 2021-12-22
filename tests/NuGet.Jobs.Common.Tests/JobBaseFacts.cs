// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Configuration;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Jobs.Common.Tests
{
    public class JobBaseFacts
    {
        private const string DefaultGalleryDbConnectionString =
            "Data Source=(localdb)\\mssqllocaldb; Initial Catalog=NuGetGallery; Integrated Security=True; MultipleActiveResultSets=True";

        private const string DefaultValidationDbConnectionString =
            "Data Source=(localdb)\\mssqllocaldb; Initial Catalog=Validation; Integrated Security=True; MultipleActiveResultSets=True";

        public class TheRegisterDatabaseMethod
        {
            [Fact]
            private void ReturnsConnectionStringBuilder_GalleryDb()
            {
                // Arrange
                var job = new TestJob();

                // Act
                var csBuilder = job.RegisterDatabase<GalleryDbConfiguration>(
                    job.ServiceContainer,
                    testConnection: false);

                // Assert
                Assert.Equal("(localdb)\\mssqllocaldb", csBuilder.DataSource);
                Assert.Equal("NuGetGallery", csBuilder.InitialCatalog);
            }

            [Fact]
            private void ReturnsConnectionStringBuilder_ValidationDb()
            {
                // Arrange
                var job = new TestJob();

                // Act
                var csBuilder = job.RegisterDatabase<ValidationDbConfiguration>(
                    job.ServiceContainer,
                    testConnection: false);

                // Assert
                Assert.Equal("(localdb)\\mssqllocaldb", csBuilder.DataSource);
                Assert.Equal("Validation", csBuilder.InitialCatalog);
            }

            [Fact]
            private void DoesNotOverwriteRegistrations()
            {
                // Arrange
                var job = new TestJob();

                // Act
                job.RegisterDatabase<GalleryDbConfiguration>(
                    job.ServiceContainer,
                    testConnection: false);

                job.RegisterDatabase<ValidationDbConfiguration>(
                    job.ServiceContainer,
                    testConnection: false);

                // Assert
                var galleryDb = job.GetDatabaseRegistration<GalleryDbConfiguration>();
                var validationDb = job.GetDatabaseRegistration<ValidationDbConfiguration>();

                Assert.NotNull(galleryDb);
                Assert.Equal("NuGetGallery", galleryDb.InitialCatalog);

                Assert.NotNull(validationDb);
                Assert.Equal("Validation", validationDb.InitialCatalog);
            }
        }

        public class TestJob : JobBase
        {
            public IServiceContainer ServiceContainer
            {
                get => MockServiceContainer.Object;
            }

            public Mock<IServiceContainer> MockServiceContainer { get; }

            public TestJob()
            {
                var mockSecretInjector = new Mock<ICachingSecretInjector>();

                var galleryOptionsSnapshot = CreateMockOptionsSnapshot(
                    new GalleryDbConfiguration {
                        ConnectionString = DefaultGalleryDbConnectionString
                    });

                var validationOptionsSnapshot = CreateMockOptionsSnapshot(
                    new ValidationDbConfiguration
                    {
                        ConnectionString = DefaultValidationDbConnectionString
                    });

                MockServiceContainer = new Mock<IServiceContainer>();

                MockServiceContainer
                    .Setup(x => x.GetService(It.IsAny<Type>()))
                    .Returns<Type>(serviceType =>
                    {
                        if (serviceType == typeof(ICachingSecretInjector))
                        {
                            return mockSecretInjector.Object;
                        }
                        else if (serviceType == typeof(IOptionsSnapshot<GalleryDbConfiguration>))
                        {
                            return galleryOptionsSnapshot.Object;
                        }
                        else if (serviceType == typeof(IOptionsSnapshot<ValidationDbConfiguration>))
                        {
                            return validationOptionsSnapshot.Object;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unexpected service lookup: {serviceType.Name}");
                        }
                    });
            }

            private Mock<IOptionsSnapshot<TDbConfiguration>> CreateMockOptionsSnapshot<TDbConfiguration>(TDbConfiguration configuration) where TDbConfiguration : class, new()
            {
                var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TDbConfiguration>>();

                mockOptionsSnapshot
                    .Setup(x => x.Value)
                    .Returns(configuration);

                return mockOptionsSnapshot;
            }

            public override void Init(
                IServiceContainer serviceContainer,
                IDictionary<string, string> jobArgsDictionary)
            {
            }

            public override Task Run()
            {
                return Task.CompletedTask;
            }
        }
    }
}
