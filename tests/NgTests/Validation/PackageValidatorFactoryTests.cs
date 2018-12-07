// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests.Validation
{
    public class PackageValidatorFactoryTests : IDisposable
    {
        private readonly HttpClientHandler _httpClientHandler = new HttpClientHandler();
        private readonly Mock<ILoggerFactory> _loggerFactory = new Mock<ILoggerFactory>();
        private const string _galleryUrl = "https://gallery.nuget.test";
        private const string _indexUrl = "https://index.nuget.test";
        private readonly TestStorageFactory _auditingStorageFactory = new TestStorageFactory();
        private readonly Func<HttpMessageHandler> _messageHandlerFactory;
        private readonly ValidatorConfiguration _validatorConfiguration = new ValidatorConfiguration(
            packageBaseAddress: "https://packagebaseaddress.nuget.test",
            requirePackageSignature: true);

        private bool _isDisposed;

        public PackageValidatorFactoryTests()
        {
            _messageHandlerFactory = () => _httpClientHandler;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _httpClientHandler.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        [Fact]
        public void Constructor_WhenLoggerFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PackageValidatorFactory(loggerFactory: null));

            Assert.Equal("loggerFactory", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_WhenGalleryUrlIsNullOrEmpty_Throws(string galleryUrl)
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            var exception = Assert.Throws<ArgumentException>(
                () => factory.Create(
                    galleryUrl,
                    _indexUrl,
                    _auditingStorageFactory,
                    Enumerable.Empty<EndpointFactory.Input>(),
                    _messageHandlerFactory,
                    _validatorConfiguration));

            Assert.Equal("galleryUrl", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Create_WhenIndexUrlIsNullOrEmpty_Throws(string indexUrl)
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            var exception = Assert.Throws<ArgumentException>(
                () => factory.Create(
                    _galleryUrl,
                    indexUrl,
                    _auditingStorageFactory,
                    Enumerable.Empty<EndpointFactory.Input>(),
                    _messageHandlerFactory,
                    _validatorConfiguration));

            Assert.Equal("indexUrl", exception.ParamName);
            Assert.StartsWith("The argument must not be null or empty.", exception.Message);
        }

        [Fact]
        public void Create_WhenAuditingStorageFactoryIsNull_Throws()
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            const StorageFactory auditingStorageFactory = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => factory.Create(
                    _galleryUrl,
                    _indexUrl,
                    auditingStorageFactory,
                    Enumerable.Empty<EndpointFactory.Input>(),
                    _messageHandlerFactory,
                    _validatorConfiguration));

            Assert.Equal("auditingStorageFactory", exception.ParamName);
        }

        [Fact]
        public void Create_WhenEndpointInputsIsNull_Throws()
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            const IEnumerable<EndpointFactory.Input> endpointInputs = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => factory.Create(
                    _galleryUrl,
                    _indexUrl,
                    _auditingStorageFactory,
                    endpointInputs,
                    _messageHandlerFactory,
                    _validatorConfiguration));

            Assert.Equal("endpointInputs", exception.ParamName);
        }

        [Fact]
        public void Create_WhenMessageHandlerFactoryIsNull_Throws()
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            const Func<HttpMessageHandler> messageHandlerFactory = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => factory.Create(
                    _galleryUrl,
                    _indexUrl,
                    _auditingStorageFactory,
                    Enumerable.Empty<EndpointFactory.Input>(),
                    messageHandlerFactory,
                    _validatorConfiguration));

            Assert.Equal("messageHandlerFactory", exception.ParamName);
        }

        [Fact]
        public void Create_WhenValidatorConfigIsNull_Throws()
        {
            var factory = new PackageValidatorFactory(_loggerFactory.Object);

            var exception = Assert.Throws<ArgumentNullException>(
                () => factory.Create(
                    _galleryUrl,
                    _indexUrl,
                    _auditingStorageFactory,
                    Enumerable.Empty<EndpointFactory.Input>(),
                    _messageHandlerFactory,
                    validatorConfig: null));

            Assert.Equal("validatorConfig", exception.ParamName);
        }
    }
}