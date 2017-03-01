// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class RuntimeServiceProviderTests
    {
        private readonly string _baseDirectoryPath;

        public RuntimeServiceProviderTests()
        {
            var thisAssemblyDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            _baseDirectoryPath = Path.GetDirectoryName(thisAssemblyDirectoryPath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_ThrowsForNullOrEmptyString(string baseDirectoryPath)
        {
            Assert.Throws<ArgumentException>(() => RuntimeServiceProvider.Create(baseDirectoryPath));
        }

        [Fact]
        public void Create_HandlesNonexistentDirectoryPath()
        {
            var baseDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var serviceProvider = RuntimeServiceProvider.Create(baseDirectoryPath);

            Assert.NotNull(serviceProvider);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var serviceProvider = RuntimeServiceProvider.Create(_baseDirectoryPath);

            serviceProvider.Dispose();
            serviceProvider.Dispose();
        }

        [Fact]
        public void GetExportedValues_ThrowsIfDisposed()
        {
            var serviceProvider = RuntimeServiceProvider.Create(_baseDirectoryPath);

            serviceProvider.Dispose();

            Assert.Throws<ObjectDisposedException>(() => serviceProvider.GetExportedValues<ExportedType>());
        }

        [Fact]
        public void GetExportedValues_ReturnsInstanceForExportedType()
        {
            using (var serviceProvider = RuntimeServiceProvider.Create(_baseDirectoryPath))
            {
                var services = serviceProvider.GetExportedValues<ExportedType>();

                Assert.NotNull(services);
                Assert.Single(services);
            }
        }

        [Fact]
        public void GetExportedValues_ReturnsEmptyEnumerableForNonExportedType()
        {
            using (var serviceProvider = RuntimeServiceProvider.Create(_baseDirectoryPath))
            {
                var services = serviceProvider.GetExportedValues<NonExportedType>();

                Assert.NotNull(services);
                Assert.Empty(services);
            }
        }

        [Export]
        [PartCreationPolicy(CreationPolicy.NonShared)]
        private class ExportedType
        {
        }

        private class NonExportedType
        {
        }
    }
}