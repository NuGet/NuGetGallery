// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class StreamExtensionsFacts
    {
        public class TheCopyToAsyncMethodWithMaxBytesWritten
        {
            [Theory]
            [InlineData(1, 1)]
            [InlineData(1, 256)]
            [InlineData(256, 256)]
            public async Task FullyReadsEmptyStream(int bufferSize, int maxBytesWritten)
            {
                InnerSource.SetLength(0);

                var result = await Source.CopyToAsync(Destination, bufferSize, maxBytesWritten, CancellationToken.None);

                Assert.Empty(Destination.ToArray());
                Assert.Equal(0, result.BytesWritten);
                Assert.Equal(0, Source.BytesRead);
                Assert.False(result.PartialRead);
            }

            [Theory]
            [InlineData(1, 1, 1)]
            [InlineData(1, 256, 1)]
            [InlineData(1, 256, 63)]
            [InlineData(1, 256, 127)]
            [InlineData(1, 1, 127)]
            [InlineData(2, 256, 1)]
            [InlineData(2, 256, 2)]
            [InlineData(2, 256, 3)]
            [InlineData(2, 256, 63)]
            [InlineData(2, 256, 64)]
            [InlineData(2, 256, 126)]
            [InlineData(2, 256, 127)]
            [InlineData(3, 256, 1)]
            [InlineData(3, 1, 1)]
            [InlineData(3, 1, 127)]
            [InlineData(200, 256, 100)]
            [InlineData(100, 256, 100)]
            public async Task PartiallyReadsStream(int chunkSize, int bufferSize, int maxBytesWritten)
            {
                Source.MaxRead = chunkSize;

                var result = await Source.CopyToAsync(Destination, bufferSize, maxBytesWritten, CancellationToken.None);

                Assert.Equal(Bytes.Take(maxBytesWritten).ToArray(), Destination.ToArray());
                Assert.Equal(maxBytesWritten, result.BytesWritten);
                Assert.Equal(maxBytesWritten + 1, Source.BytesRead);
                Assert.True(result.PartialRead);
            }

            [Theory]
            [InlineData(1, 1, 128)]
            [InlineData(1, 256, 128)]
            [InlineData(2, 256, 128)]
            [InlineData(127, 256, 128)]
            [InlineData(128, 256, 128)]
            [InlineData(129, 256, 128)]
            [InlineData(1, 256, 129)]
            [InlineData(2, 1, 129)]
            [InlineData(2, 256, 129)]
            [InlineData(127, 256, 129)]
            [InlineData(128, 256, 129)]
            [InlineData(129, 256, 129)]
            [InlineData(1, 256, 130)]
            [InlineData(2, 256, 130)]
            [InlineData(127, 1, 130)]
            [InlineData(127, 256, 130)]
            [InlineData(128, 1, 130)]
            [InlineData(128, 256, 130)]
            [InlineData(129, 1, 130)]
            [InlineData(129, 256, 130)]
            public async Task FullyReadsStream(int chunkSize, int bufferSize, int maxBytesWritten)
            {
                Source.MaxRead = chunkSize;

                var result = await Source.CopyToAsync(Destination, bufferSize, maxBytesWritten, CancellationToken.None);

                Assert.Equal(Bytes.Take(maxBytesWritten).ToArray(), Destination.ToArray());
                Assert.Equal(InnerSource.Length, result.BytesWritten);
                Assert.Equal(InnerSource.Length, Source.BytesRead);
                Assert.False(result.PartialRead);
            }

            public TheCopyToAsyncMethodWithMaxBytesWritten()
            {
                Bytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
                InnerSource = new MemoryStream(Bytes);
                Source = new MaxReadStream(InnerSource, 1);
                Destination = new MemoryStream();
            }

            public byte[] Bytes { get; }
            public MemoryStream InnerSource { get; }
            public MaxReadStream Source { get; set; }
            public MemoryStream Destination { get; }
        }

        public class MaxReadStream : Stream
        {
            private readonly Stream _innerStream;

            public MaxReadStream(Stream innerStream, int maxRead)
            {
                _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
                MaxRead = maxRead;
                BytesRead = 0;
            }


            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _innerStream.Read(buffer, offset, Math.Min(MaxRead, count));
                BytesRead += read;
                return read;
            }

            public int MaxRead { get; set; }
            public int BytesRead { get; private set; }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;

            public override long Position
            {
                get => _innerStream.Position;
                set => _innerStream.Position = value;
            }

            public override void Flush() => _innerStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
        }
    }
}
