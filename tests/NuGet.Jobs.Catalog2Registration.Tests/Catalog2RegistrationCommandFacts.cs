// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Moq.Protected;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class Catalog2RegistrationCommandFacts
    {
        public class TheExecuteAsyncMethod : Facts
        {
            public TheExecuteAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task LoadsCursorsAndExecutesCollector()
            {
                await Target.ExecuteAsync();

                HttpMessageHandler.Verify(
                    x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                CloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Never);
                Storage.Protected().Verify(
                    "OnLoadAsync",
                    Times.Once(),
                    ItExpr.IsAny<Uri>(),
                    ItExpr.IsAny<CancellationToken>());
                Storage.Protected().Verify(
                    "OnLoadAsync",
                    Times.Once(),
                    new Uri("https://example/azs/cursor.json"),
                    ItExpr.IsAny<CancellationToken>());
                Collector.Verify(
                    x => x.RunAsync(It.IsAny<DurableCursor>(), It.Is<MemoryCursor>(c => c.Value == DateTime.MaxValue.ToUniversalTime()), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task CreatesContainersIfConfigured()
            {
                Config.CreateContainers = true;

                await Target.ExecuteAsync();

                CloudBlobClient.Verify(x => x.GetContainerReference(It.IsAny<string>()), Times.Exactly(3));
                CloudBlobClient.Verify(x => x.GetContainerReference(Config.LegacyStorageContainer), Times.Once);
                CloudBlobClient.Verify(x => x.GetContainerReference(Config.GzippedStorageContainer), Times.Once);
                CloudBlobClient.Verify(x => x.GetContainerReference(Config.SemVer2StorageContainer), Times.Once);
                CloudBlobContainer.Verify(x => x.CreateIfNotExistAsync(It.Is<BlobContainerPermissions>(p => p.PublicAccess == BlobContainerPublicAccessType.Blob)), Times.Exactly(3));
            }

            [Fact]
            public async Task LoadsDependencyCursorsIfConfigured()
            {
                Config.DependencyCursorUrls = new List<string>()
                {
                    "https://example/fc-1/cursor.json",
                    "https://example/fc-2/cursor.json",
                };
                HttpMessageHandler
                    .Setup(x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"value\": \"{DateTimeOffset.UtcNow.ToString("O")}\"}}"),
                    });

                await Target.ExecuteAsync();

                HttpMessageHandler.Verify(
                    x => x.OnSendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(2));
                HttpMessageHandler.Verify(
                    x => x.OnSendAsync(
                        It.Is<HttpRequestMessage>(r => r.RequestUri.AbsoluteUri == "https://example/fc-1/cursor.json"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                HttpMessageHandler.Verify(
                    x => x.OnSendAsync(
                        It.Is<HttpRequestMessage>(r => r.RequestUri.AbsoluteUri == "https://example/fc-2/cursor.json"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
                Storage.Protected().Verify(
                    "OnLoadAsync",
                    Times.Once(),
                    new Uri("https://example/azs/cursor.json"),
                    ItExpr.IsAny<CancellationToken>());
                Collector.Verify(
                    x => x.RunAsync(It.IsAny<DurableCursor>(), It.IsAny<AggregateCursor>(), It.IsAny<CancellationToken>()),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Collector = new Mock<ICollector>();
                CloudBlobClient = new Mock<ICloudBlobClient>();
                StorageFactory = new Mock<IStorageFactory>();
                HttpMessageHandler = new Mock<TestHttpMessageHandler> { CallBase = true };
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Logger = output.GetLogger<Catalog2RegistrationCommand>();

                CloudBlobContainer = new Mock<ICloudBlobContainer>();
                CloudBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(() => CloudBlobContainer.Object);
                Storage = new Mock<Storage>(new Uri("https://example/azs"));
                StorageFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(() => Storage.Object);
                Config = new Catalog2RegistrationConfiguration
                {
                    StorageConnectionString = "UseDevelopmentStorage=true",
                    LegacyStorageContainer = "reg",
                    GzippedStorageContainer = "reg-gz",
                    SemVer2StorageContainer = "reg-gz-semver2",
                };
                Options.Setup(x => x.Value).Returns(() => Config);

                Target = new Catalog2RegistrationCommand(
                    Collector.Object,
                    CloudBlobClient.Object,
                    StorageFactory.Object,
                    () => HttpMessageHandler.Object,
                    Options.Object,
                    Logger);
            }

            public Mock<ICollector> Collector { get; }
            public Mock<ICloudBlobClient> CloudBlobClient { get; }
            public Mock<IStorageFactory> StorageFactory { get; }
            public Mock<TestHttpMessageHandler> HttpMessageHandler { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public RecordingLogger<Catalog2RegistrationCommand> Logger { get; }
            public Mock<Storage> Storage { get; }
            public Mock<ICloudBlobContainer> CloudBlobContainer { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public Catalog2RegistrationCommand Target { get; }
        }
    }
}
