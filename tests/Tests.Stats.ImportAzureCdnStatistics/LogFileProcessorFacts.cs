// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Stats.AzureCdnLogs.Common;
using Stats.ImportAzureCdnStatistics;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class LogFileProcessorFacts
    {
        private static readonly Assembly _currentAssembly =
            typeof(LogFileProcessorFacts).Assembly;

        private static readonly PackageTranslator _packageTranslator = new PackageTranslator();

        private static readonly IPackageStatisticsParser _packageStatisticsParser =
            new PackageStatisticsParser(_packageTranslator, new LoggerFactory());

        public class WhenOnlyPackageStatisticsInLogFile
        {
            private const string _logFileName = "PackageDownloads.log";
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILeasedLogFile _leasedLogFile;

            public WhenOnlyPackageStatisticsInLogFile(ITestOutputHelper testOutputHelper)
            {
                var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new XunitLoggerProvider(testOutputHelper));
                _loggerFactory = loggerFactory;

                _leasedLogFile = GetLeasedLogFileMock(_logFileName);
            }

            [Fact]
            public async Task ImportsFactsAndAggregatesWhenNotProcessingAggregatesOnlyAndNotAlreadyImported()
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName))
                    .Returns(() => Task.FromResult(0));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: false);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotImportFactsWhenProcessingAggregatesOnly(bool hasImportedPackageStatistics)
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(hasImportedPackageStatistics));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: true);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotImportFactsWhenAlreadyImportedButStillProcessesAggregates(bool aggregatesOnly)
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);

                if (aggregatesOnly)
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Never);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Never);
                }
                else
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
                }
            }
        }

        public class WhenOnlyToolStatisticsInLogFile
        {
            private const string _logFileName = "ToolDownloads.log";
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILeasedLogFile _leasedLogFile;

            public WhenOnlyToolStatisticsInLogFile(ITestOutputHelper testOutputHelper)
            {
                var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new XunitLoggerProvider(testOutputHelper));
                _loggerFactory = loggerFactory;

                _leasedLogFile = GetLeasedLogFileMock(_logFileName);
            }

            [Fact]
            public async Task ImportsFactsButNoAggregatesWhenNotProcessingAggregatesOnlyAndNotAlreadyImported()
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: false);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotImportFactsWhenProcessingAggregatesOnly(bool hasImportedToolStatistics)
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(hasImportedToolStatistics));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: true);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotImportFactsWhenAlreadyImported(bool aggregatesOnly)
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Never);

                if (aggregatesOnly)
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Never);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Never);
                }
                else
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
                }
            }
        }

        public class WhenPackageAndToolStatisticsInLogFile
        {
            private const string _logFileName = "PackageAndToolDownloads.log";
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILeasedLogFile _leasedLogFile;

            public WhenPackageAndToolStatisticsInLogFile(ITestOutputHelper testOutputHelper)
            {
                var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new XunitLoggerProvider(testOutputHelper));
                _loggerFactory = loggerFactory;

                _leasedLogFile = GetLeasedLogFileMock(_logFileName);
            }

            [Fact]
            public async Task ImportsFactsAndAggregatesWhenNotProcessingAggregatesOnlyAndNothingAlreadyImported()
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName))
                    .Returns(() => Task.FromResult(0));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: false);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Exactly(2));
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
            }

            [Fact]
            public async Task ImportsPackageFactsAndPakageAggregatesWhenNotProcessingAggregatesOnlyAndToolFactsAlreadyImported()
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName))
                    .Returns(() => Task.FromResult(0));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: false);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
            }

            [Fact]
            public async Task ImportToolFactsAndPackageAggregatesWhenNotProcessingAggregatesOnlyAndPackageFactsAlreadyImported()
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(false));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName))
                    .Returns(() => Task.FromResult(0));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly: false);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<ToolStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);
                statisticsBlobContainerUtilityMock
                    .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotImportFactsWhenAlreadyImportedButDoesImportPackageAggregates(bool aggregatesOnly)
            {
                // arrange
                var statisticsBlobContainerUtilityMock = new Mock<IStatisticsBlobContainerUtility>(MockBehavior.Strict);
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.OpenCompressedBlobAsync(It.IsAny<ILeasedLogFile>()))
                    .Returns(OpenLeasedLogFileStream(_logFileName));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.ArchiveBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));
                statisticsBlobContainerUtilityMock
                    .Setup(m => m.DeleteSourceBlobAsync(_leasedLogFile))
                    .Returns(Task.FromResult(0));

                var warehouseMock = new Mock<IStatisticsWarehouse>(MockBehavior.Strict);
                warehouseMock
                    .Setup(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName))
                    .Returns(Task.FromResult(true));
                warehouseMock
                    .Setup(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName))
                    .Returns(Task.FromResult(new DataTable()));
                warehouseMock
                    .Setup(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()))
                    .Returns(() => Task.FromResult(0));

                var logFileProcessor = new LogFileProcessor(
                    statisticsBlobContainerUtilityMock.Object,
                    _loggerFactory,
                    warehouseMock.Object);

                // act
                await logFileProcessor.ProcessLogFileAsync(_leasedLogFile, _packageStatisticsParser, aggregatesOnly);

                // assert
                warehouseMock
                    .Verify(m => m.HasImportedPackageStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.HasImportedToolStatisticsAsync(_leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.CreateAsync(It.IsAny<IReadOnlyCollection<PackageStatistics>>(), _leasedLogFile.BlobName), Times.Once);
                warehouseMock
                    .Verify(m => m.InsertDownloadFactsAsync(It.IsAny<DataTable>(), _leasedLogFile.BlobName), Times.Never);
                warehouseMock
                    .Verify(m => m.StoreLogFileAggregatesAsync(It.IsAny<LogFileAggregates>()), Times.Once);

                if (aggregatesOnly)
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Never);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Never);
                }
                else
                {
                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.ArchiveBlobAsync(_leasedLogFile), Times.Once);

                    statisticsBlobContainerUtilityMock
                        .Verify(m => m.DeleteSourceBlobAsync(_leasedLogFile), Times.Once);
                }
            }
        }

        private static ILeasedLogFile GetLeasedLogFileMock(string logFileName)
        {
            var logFileMock = new Mock<ILeasedLogFile>();
            logFileMock.Setup(m => m.BlobName).Returns(logFileName);
            logFileMock.Setup(m => m.Uri).Returns($"http://127.0.0.1/logs/{logFileName}");
            var logFile = logFileMock.Object;
            return logFile;
        }

        private static Task<Stream> OpenLeasedLogFileStream(string logFileName)
        {
            var resourceStream = _currentAssembly.GetManifestResourceStream(
                $"Tests.Stats.ImportAzureCdnStatistics.TestData.{logFileName}");

            return Task.FromResult(resourceStream);
        }
    }
}