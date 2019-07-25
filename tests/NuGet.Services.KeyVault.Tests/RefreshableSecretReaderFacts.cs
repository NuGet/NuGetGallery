// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class RefreshableSecretReaderFacts
    {
        public class RefreshAsync : Facts
        {
            [Fact]
            public async Task DoesNothingWithEmptyCache()
            {
                await Target.RefreshAsync(CancellationToken.None);

                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task RefreshesAllNames()
            {
                await Target.GetSecretAsync(SecretNameA);
                await Target.GetSecretAsync(SecretNameB);
                SecretReader.Invocations.Clear();

                await Target.RefreshAsync(CancellationToken.None);

                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(SecretNameA), Times.Once);
                SecretReader.Verify(x => x.GetSecretObjectAsync(SecretNameB), Times.Once);
            }

            [Fact]
            public async Task CachesLatestValue()
            {
                await Target.GetSecretAsync(SecretNameA);
                SecretReader.Setup(x => x.GetSecretObjectAsync(SecretNameA)).ReturnsAsync(() => SecretB.Object);

                await Target.RefreshAsync(CancellationToken.None);

                var secretObject = await Target.GetSecretObjectAsync(SecretNameA);
                Assert.Same(SecretB.Object, secretObject);
            }

            [Fact]
            public async Task RespectsTheToken()
            {
                await Target.GetSecretAsync(SecretNameA);
                SecretReader.Invocations.Clear();
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                cts.Cancel();

                await Target.RefreshAsync(token);

                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(It.IsAny<string>()), Times.Never);
            }
        }

        public class GetSecretObjectAsync : Facts
        {
            [Fact]
            public async Task FetchesAnUncachedSecret()
            {
                var actual = await Target.GetSecretObjectAsync(SecretNameA);

                Assert.Same(SecretA.Object, actual);
                SecretReader.Verify(x => x.GetSecretObjectAsync(SecretNameA), Times.Once);
            }

            [Fact]
            public async Task ReturnsACompletedTaskIfAlreadyCached()
            {
                await Target.GetSecretObjectAsync(SecretNameA);
                SecretReader.Invocations.Clear();

                var task = Target.GetSecretObjectAsync(SecretNameA);

                Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            }

            [Fact]
            public async Task DoesNotSwitchThreadIfAlreadyCached()
            {
                await Target.GetSecretObjectAsync(SecretNameA);
                SecretReader.Invocations.Clear();
                var threadId = Thread.CurrentThread.ManagedThreadId;

                await Target.GetSecretObjectAsync(SecretNameA);

                Assert.Equal(threadId, Thread.CurrentThread.ManagedThreadId);
                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task ThrowsIfReadsAreBlocked()
            {
                Settings.BlockUncachedReads = true;

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.GetSecretAsync(SecretNameA));
                Assert.Equal($"The secret '{SecretNameA}' is not cached.", ex.Message);
            }
        }

        public class GetSecretAsync : Facts
        {
            [Fact]
            public async Task FetchesAnUncachedSecret()
            {
                var actual = await Target.GetSecretAsync(SecretNameA);

                Assert.Same(SecretA.Object.Value, actual);
                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(SecretNameA), Times.Once);
            }

            [Fact]
            public async Task ReturnsACompletedTaskIfAlreadyCached()
            {
                await Target.GetSecretAsync(SecretNameA);

                var task = Target.GetSecretAsync(SecretNameA);

                Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            }

            [Fact]
            public async Task DoesNotSwitchThreadIfAlreadyCached()
            {
                await Target.GetSecretAsync(SecretNameA);
                SecretReader.Invocations.Clear();
                var threadId = Thread.CurrentThread.ManagedThreadId;

                await Target.GetSecretAsync(SecretNameA);

                Assert.Equal(threadId, Thread.CurrentThread.ManagedThreadId);
                SecretReader.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
                SecretReader.Verify(x => x.GetSecretObjectAsync(It.IsAny<string>()), Times.Never);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                SecretReader = new Mock<ISecretReader>();
                Cache = new ConcurrentDictionary<string, ISecret>();
                Settings = new RefreshableSecretReaderSettings();

                SecretNameA = "A-Name";
                SecretNameB = "B-Name";

                SecretA = new Mock<ISecret>();
                SecretB = new Mock<ISecret>();

                SecretA.Setup(x => x.Value).Returns("A-value");
                SecretB.Setup(x => x.Value).Returns("B-value");

                SecretReader
                    .Setup(x => x.GetSecretObjectAsync(SecretNameA))
                    .Returns(async () =>
                    {
                        await Task.Yield();
                        return SecretA.Object;
                    });
                SecretReader
                    .Setup(x => x.GetSecretObjectAsync(SecretNameB))
                    .Returns(async () =>
                    {
                        await Task.Yield();
                        return SecretB.Object;
                    });

                Target = new RefreshableSecretReader(
                    SecretReader.Object,
                    Cache,
                    Settings);
            }

            public Mock<ISecretReader> SecretReader { get; }
            public ConcurrentDictionary<string, ISecret> Cache { get; }
            public RefreshableSecretReaderSettings Settings { get; }
            public string SecretNameA { get; }
            public string SecretNameB { get; }
            public Mock<ISecret> SecretA { get; }
            public Mock<ISecret> SecretB { get; }
            public RefreshableSecretReader Target { get; }
        }
    }
}
