// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.Data.Edm.Library.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Storage;
using Xunit;

namespace Stats.PostProcessReports.Tests
{
    public class DetailedReportPostProcessorFacts
    {
        private Mock<IStorage> _sourceStorageMock = new Mock<IStorage>();
        private Mock<IStorage> _workStorageMock = new Mock<IStorage>();
        private Mock<IStorage> _destinationStorageMock = new Mock<IStorage>();
        private PostProcessReportsConfiguration _configuration = new PostProcessReportsConfiguration();
        private Mock<IOptionsSnapshot<PostProcessReportsConfiguration>> _configurationMock = new Mock<IOptionsSnapshot<PostProcessReportsConfiguration>>();
        private Mock<ITelemetryService> _telemetryServiceMock = new Mock<ITelemetryService>();
        private Mock<ILogger<DetailedReportPostProcessor>> _loggerMock = new Mock<ILogger<DetailedReportPostProcessor>>();
        private DetailedReportPostProcessor _target;

        private List<string> _sourceFiles = new List<string>();
        private List<string> _workFiles = new List<string>();
        private List<string> _destinationFiles = new List<string>();

        [Fact]
        public async Task DoesntStartIfNoSuccessFile()
        {
            _sourceStorageMock
                .Setup(ss => ss.List(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<StorageListItem>)new List<StorageListItem>());

            await _target.CopyReportsAsync();

            _sourceStorageMock
                .Verify(ss => ss.List(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            _sourceStorageMock.VerifyNoOtherCalls();
            _workStorageMock.VerifyNoOtherCalls();
            _destinationStorageMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task DoesntCopyIfCopySucceeded()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json"
            };

            _workFiles = new List<string>
            {
                "_WorkCopySucceeded",
                "file1.json"
            };

            await _target.CopyReportsAsync();

            _sourceStorageMock
                .Verify(ss => ss.CopyAsync(It.IsAny<Uri>(), It.IsAny<IStorage>(), It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
            _workStorageMock
                .Verify(ws => ws.Delete(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CleansWorkingStoreAndCopiesIfSourceFilesDontMatch()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json"
            };

            _workFiles = new List<string>
            {
                "_WorkCopySucceeded",
                "file0.json" // different file name
            };

            await _target.CopyReportsAsync();

            _sourceStorageMock
                .Verify(ss => ss.CopyAsync(It.IsAny<Uri>(), It.IsAny<IStorage>(), It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            _workStorageMock
                .Verify(ws => ws.Delete(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CleansWorkingStoreWhenNeedsToCopy()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json"
            };

            _workFiles = new List<string>
            {
                "random.json",
                "random.extension",
                "third.file"
            };

            var filesToDelete = _workFiles.Count;

            await _target.CopyReportsAsync();

            _workStorageMock
                .Verify(ws => ws.Delete(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Exactly(filesToDelete));
        }

        [Fact]
        public async Task CopiesNewJsonFiles()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json",
                "file2.json",
                "file3.txt"
            };
            var filesToCopy = _sourceFiles.Count(f => f.EndsWith(".json"));

            await _target.CopyReportsAsync();

            _sourceStorageMock
                .Verify(ss => ss.CopyAsync(It.IsAny<Uri>(), It.IsAny<IStorage>(), It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Exactly(filesToCopy));
        }

        [Fact]
        public async Task NoOpsIfAlreadyProcessed()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json"
            };

            _workFiles = new List<string>
            {
                "_WorkCopySucceeded",
                "_JobSucceeded",
                "file1.json"
            };

            var filesToDelete = _workFiles.Count;

            await _target.CopyReportsAsync();

            _sourceStorageMock
                .Verify(ss => ss.CopyAsync(It.IsAny<Uri>(), It.IsAny<IStorage>(), It.IsAny<Uri>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
            _workStorageMock
                .Verify(ws => ws.Delete(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
            _destinationStorageMock
                .Verify(ds => ds.Save(It.IsAny<Uri>(), It.IsAny<StorageContent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SkipsProcessedFiles()
        {
            const string file1 = "file1.json";
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                file1,
                "file2.json"
            };

            _workFiles = new List<string>
            {
                "_WorkCopySucceeded",
                file1,
                "file2.json"
            };

            var file1Metadata = new Dictionary<string, string>
            {
                { "TotalLines", "123" },
                { "LinesFailed", "0" },
                { "FilesCreated", "123" }
            };
            _workStorageMock
                .Setup(ss => ss.List(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (IEnumerable<StorageListItem>)new List<StorageListItem>(_workFiles.Select(f => Blob(
                    _workStorageMock,
                    f,
                    f == file1 ? file1Metadata : null))));

            var file1Uri = Blob(_workStorageMock, file1).Uri;

            await _target.CopyReportsAsync();

            _workStorageMock
                .Verify(ws => ws.Load(file1Uri, It.IsAny<CancellationToken>()), Times.Never);
            _workStorageMock
                .Verify(ws => ws.Load(It.Is<Uri>(f => f != file1Uri), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SplitsIncomingFiles()
        {
            _sourceFiles = new List<string>
            {
                "_SUCCESS",
                "file1.json"
            };

            _workFiles = new List<string>
            {
                "_WorkCopySucceeded",
                "file1.json"
            };

            var fileUrl = Blob(_workStorageMock, "file1.json").Uri;
            const string line1 = "{\"PackageId\": \"Foo\", \"Otherstuff\": 123}";
            const string line2 = "{\"PackageId\": \"Bar\", \"Otherstuff\": 321}";
            const string content = line1 + "\n" + line2 + "\n";

            _workStorageMock
                .Setup(ws => ws.Load(fileUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StringStorageContent(content));

            await _target.CopyReportsAsync();

            _destinationStorageMock
                .Verify(
                    ds => ds.Save(
                        It.Is<Uri>(u => u.AbsolutePath.EndsWith("recentpopularitydetail_foo.json")),
                        It.Is<StringStorageContent>(sc => sc.Content.StartsWith(line1)),
                        true, It.IsAny<CancellationToken>()),
                    Times.Once);
            _destinationStorageMock
                .Verify(
                    ds => ds.Save(
                        It.Is<Uri>(u => u.AbsolutePath.EndsWith("recentpopularitydetail_bar.json")),
                        It.Is<StringStorageContent>(sc => sc.Content.StartsWith(line2)),
                        true, It.IsAny<CancellationToken>()),
                    Times.Once);
        }

        public DetailedReportPostProcessorFacts()
        {
            SetupStorageMock(_sourceStorageMock, "https://storage.test/source/directory", () => _sourceFiles);
            SetupStorageMock(_workStorageMock, "https://storage.test/work/directory", () => _workFiles);
            SetupStorageMock(_destinationStorageMock, "https://storage.test/destination/directory", () => _destinationFiles);

            _configuration.ReportWriteDegreeOfParallelism = 1;

            _configurationMock
                .SetupGet(c => c.Value)
                .Returns(_configuration);

            _target = new DetailedReportPostProcessor(
                _sourceStorageMock.Object,
                _workStorageMock.Object,
                _destinationStorageMock.Object,
                _configurationMock.Object,
                _telemetryServiceMock.Object,
                _loggerMock.Object);
        }

        private static StorageListItem Blob(Mock<IStorage> storageMock, string filename, IDictionary<string, string> metadata = null)
        {
            var blobUri = new Uri(storageMock.Object.BaseAddress, filename);
            return new StorageListItem(blobUri, DateTime.UtcNow, metadata);
        }

        private static void SetupStorageMock(Mock<IStorage> mock, string baseUrl, Func<List<string>> files)
        {
            mock
                .SetupGet(s => s.BaseAddress)
                .Returns(new Uri(baseUrl));
            mock
                .Setup(s => s.Load(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((StorageContent)new StringStorageContent(""));
            mock
                .Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string filename, CancellationToken _) =>
                {
                    return files().Contains(filename);
                });
            mock
                .Setup(s => s.List(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (IEnumerable<StorageListItem>)new List<StorageListItem>(files().Select(f => Blob(mock, f))));
            mock
                .Setup(s => s.ResolveUri(It.IsAny<string>()))
                .Returns((string f) => Blob(mock, f).Uri);
        }
    }
}
