// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;
using Stats.CDNLogsSanitizer;
using Moq;
using Xunit;


namespace Tests.Stats.CDNLogsSanitizer
{
    public class ProcessorTests
    {
        public class ConstructorTests
        {
            [Fact]
            public void NullArgumentCheck()
            {
                // Arrange
                var _logSource = new Mock<ILogSource>();
                var _logDestination = new Mock<ILogDestination>();
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();

                // Act + Assert
                Assert.Throws<ArgumentNullException>(() => new Processor(null, _logDestination.Object, 3, sanitizerList, logger.Object));
                Assert.Throws<ArgumentNullException>(() => new Processor(_logSource.Object, null, 3, sanitizerList, logger.Object));
                Assert.Throws<ArgumentNullException>(() => new Processor(_logSource.Object, _logDestination.Object, 3, null, logger.Object));
                Assert.Throws<ArgumentNullException>(() => new Processor(_logSource.Object, _logDestination.Object, 3, sanitizerList, null));

            }
        }

        public class ProcessAsyncTests
        {
            [Fact]
            public async Task OnCancellationTheEntireExecutionIsStopped()
            {
                // Arrange
                var _logSource = new Mock<ILogSource>();
                var _logDestination = new Mock<ILogDestination>();
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Cancel();
                var processor = new Processor(_logSource.Object, _logDestination.Object, 1, sanitizerList, logger.Object);

                // Act 
                await processor.ProcessAsync(cancellationTokenSource.Token);

                // Assert
                _logSource.Verify(logS => logS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task TheProcessOfABlobIsStoppedOnCancellation()
            {
                // Arrange
                // Set a source that returns 1 element to process
                var _logSource = new Mock<ILogSource>();
                var dummyLockResult = new AzureBlobLockResult(new CloudBlob(new Uri("https://dummy/foo.gz")), true, "leaseid", CancellationToken.None);
                dummyLockResult.BlobOperationToken.Cancel();
                _logSource.Setup(lS => lS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .ReturnsAsync(new Uri[] { new Uri("https://dummy") });
                _logSource.Setup(lS => lS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(dummyLockResult);

                // set a number larger than 1
                int maxElementsToProcess = 2;
                var _logDestination = new Mock<ILogDestination>();
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var processor = new Processor(_logSource.Object, _logDestination.Object, maxElementsToProcess, sanitizerList, logger.Object);

                    // Act 
                    await processor.ProcessAsync(cancellationTokenSource.Token);

                    // Assert
                    _logSource.Verify(logS => logS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
                    _logSource.Verify(logS => logS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
                    _logSource.Verify(logS => logS.OpenReadAsync(It.IsAny<Uri>(), ContentType.GZip, It.IsAny<CancellationToken>()), Times.Never);
                }
            }

            [Fact]
            public async Task IfTheSourceDoesNotHaveMoreElementsTheExecutionWillStop()
            {
                // Arrange
                // Set a source that returns 1 element to process
                var uri = "https://dummy/foo.gz";
                var cb = new CloudBlob(new Uri(uri));
                var _logSource = new Mock<ILogSource>();
                var dummyLockResult = new AzureBlobLockResult(cb, false, "leaseid", CancellationToken.None);
                _logSource.Setup(lS => lS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>())).ReturnsAsync(new Uri[] { new Uri(uri) });
                _logSource.Setup(lS => lS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(dummyLockResult);

                // set a number larger than 1
                int maxElementsToProcess = 2;
                var _logDestination = new Mock<ILogDestination>();
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var processor = new Processor(_logSource.Object, _logDestination.Object, maxElementsToProcess, sanitizerList, logger.Object);

                    // Act 
                    await processor.ProcessAsync(cancellationTokenSource.Token);

                    // Assert
                    _logSource.Verify(logS => logS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
                    _logSource.Verify(logS => logS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
                }
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]

            public async Task VerifyCleanAsyncCallOnErrorIsInvokedWhenWriteAsyncThrows(bool writeAsyncThrows)
            {
                // Arrange
                // Set a source that returns 1 element to process
                var _logSource = new Mock<ILogSource>();
                var dummyLockResult = new AzureBlobLockResult(new CloudBlob(new Uri("https://dummy/foo.gz")), true, "leaseid", CancellationToken.None);
                _logSource.Setup(lS => lS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .ReturnsAsync(new Uri[] { new Uri("https://dummy") });
                _logSource.Setup(lS => lS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(dummyLockResult);
                _logSource.Setup(lS => lS.OpenReadAsync(It.IsAny<Uri>(), ContentType.GZip, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MemoryStream());

                // set a number larger than 1
                int maxElementsToProcess = 2;
                var _logDestination = new Mock<ILogDestination>();
                var writeResult = writeAsyncThrows ? new AsyncOperationResult(null, new Exception("Boo")) : new AsyncOperationResult(true, null);
                _logDestination.Setup(lD => lD.TryWriteAsync(It.IsAny<Stream>(), It.IsAny<Action<Stream, Stream>>(), It.IsAny<string>(), ContentType.GZip, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(writeResult);
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var processor = new Processor(_logSource.Object, _logDestination.Object, maxElementsToProcess, sanitizerList, logger.Object);

                    // Act 
                    await processor.ProcessAsync(cancellationTokenSource.Token);

                    // Assert
                    _logSource.Verify(logS => logS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
                    _logSource.Verify(logS => logS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Once);
                    _logSource.Verify(logS => logS.TryCleanAsync(dummyLockResult, writeAsyncThrows, It.IsAny<CancellationToken>()), Times.Once);
                    _logSource.Verify(logS => logS.TryReleaseLockAsync(dummyLockResult, It.IsAny<CancellationToken>()), Times.Once);
                }
            }

            [Fact]
            public async Task WhenLockIsNotTakenTheExecutionDoesNotProceed()
            {
                // Arrange 
                int maxElementsToProcess = 1;
                var _logSource = new Mock<ILogSource>();
                var dummyLockResult = new AzureBlobLockResult(new CloudBlob(new Uri("https://dummy/foo.gz")), false, "leaseid", CancellationToken.None);
                _logSource.Setup(lS => lS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                    .ReturnsAsync(new Uri[] { new Uri("https://dummy/foo.gz"), new Uri("https://dummy/foo2.gz") });
                _logSource.Setup(lS => lS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(dummyLockResult);
                var _logDestination = new Mock<ILogDestination>();
                var sanitizerList = new List<ISanitizer>();
                var logger = new Mock<ILogger<Processor>>();
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var processor = new Processor(_logSource.Object, _logDestination.Object, maxElementsToProcess, sanitizerList, logger.Object);

                    // Act 
                    await processor.ProcessAsync(cancellationTokenSource.Token);

                    // Assert
                    // This will be invoked only once because the continuation should not continue after
                    _logSource.Verify(logS => logS.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
                    _logSource.Verify(logS => logS.TakeLockAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
                    _logSource.Verify(logS => logS.OpenReadAsync(It.IsAny<Uri>(), ContentType.GZip, It.IsAny<CancellationToken>()), Times.Never);
                }
            }
        }

        public class ProcessStreamTests
        {
            [Fact]
            public void ProcessStreamProcessAnInputToAnOutput()
            {
                List<string> result = new List<string>();
                var header = "dummy0,c-ip,dummy1";
                var sanitizer = new ClientIPSanitizer(new LogHeaderMetadata(header, ','));
                using (var outStream = new MemoryStream())
                {
                    using (var inputStream = new MemoryStream())
                    {
                        using (var sourceStreamWriter = new StreamWriter(inputStream, Encoding.UTF8, 1024, true))
                        {
                            sourceStreamWriter.WriteLine(header);
                            sourceStreamWriter.WriteLine("one,100.23.45.11,two");
                            sourceStreamWriter.Flush();
                        }
                        inputStream.Position = 0;
                        var _logSource = new Mock<ILogSource>();
                        int maxElementsToProcess = 1;
                        var _logDestination = new Mock<ILogDestination>();
                        var sanitizerList = new List<ISanitizer>{ sanitizer };
                        var logger = new Mock<ILogger<Processor>>();
                        using (var cancellationTokenSource = new CancellationTokenSource())
                        {
                            var processor = new Processor(_logSource.Object, _logDestination.Object, maxElementsToProcess, sanitizerList, logger.Object);

                            // Act 
                            processor.ProcessStream(inputStream, outStream);
                        }
                    }
                    outStream.Position = 0;

                    using (var targetStreamReader = new StreamReader(outStream))
                    {
                        while (!targetStreamReader.EndOfStream)
                        {
                            result.Add(targetStreamReader.ReadLine());
                        }
                        Assert.Equal(2, result.Count);
                        Assert.Equal(header, result[0]);
                        Assert.Equal("one,100.23.45.0,two", result[1]);
                    }

                }                
            }
        }

    }
}
