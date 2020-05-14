// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Validation.Symbols.Tests;
using Xunit;

namespace NuGet.Tools.SplitLargeFiles
{
    public class ProgramFacts : IDisposable
    {
        private static readonly string BR = Environment.NewLine;

        public ProgramFacts()
        {
            TestDirectory = TestDirectory.Create();
            FileName = "input.log";
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AllowsCustomSuffix(bool gzip)
        {
            var input = $"aaaa{BR}bbbb{BR}cccc{BR}";
            WriteFile(input, gzip);

            var exitCode = Program.Main(new[] { FilePath, (FileSize - 1).ToString(), "{0:D4}" });

            Assert.Equal(0, exitCode);
            Assert.Equal(input, ReadAllText("input.log", gzip));
            Assert.Equal($"aaaa{BR}", ReadAllText("input0000.log", gzip));
            Assert.Equal($"bbbb{BR}cccc{BR}", ReadAllText("input0001.log", gzip));
            AssertFileCount(3);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SplitsFileByLines(bool gzip)
        {
            var input = $"aaaa{BR}bbbb{BR}cccc{BR}";
            WriteFile(input, gzip);

            var exitCode = Program.Main(new[] { FilePath, (FileSize - 1).ToString() });

            Assert.Equal(0, exitCode);
            Assert.Equal(input, ReadAllText("input.log", gzip));
            Assert.Equal($"aaaa{BR}", ReadAllText("input_0000.log", gzip));
            Assert.Equal($"bbbb{BR}cccc{BR}", ReadAllText("input_0001.log", gzip));
            AssertFileCount(3);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WritesSingleFileIfDesiredSizeIsSame(bool gzip)
        {
            var input = $"aaaa{BR}bbbb{BR}cccc{BR}";
            WriteFile(input, gzip);

            var exitCode = Program.Main(new[] { FilePath, FileSize.ToString() });

            Assert.Equal(0, exitCode);
            Assert.Equal(input, ReadAllText("input.log", gzip));
            Assert.Equal(input, ReadAllText("input_0000.log", gzip));
            AssertFileCount(2);
        }

        [Fact]
        public void WritesEmptyFileIfInputIsEmpty()
        {
            WriteFile(string.Empty);

            var exitCode = Program.Main(new[] { FilePath, "20" });

            Assert.Equal(0, exitCode);
            Assert.Empty(ReadAllText("input.log"));
            Assert.Empty(ReadAllText("input_0000.log"));
            AssertFileCount(2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WritesEmptyLine(bool gzip)
        {
            var input = $"{BR}";
            WriteFile(input, gzip);

            var exitCode = Program.Main(new[] { FilePath, FileSize.ToString() });

            Assert.Equal(0, exitCode);
            Assert.Equal(input, ReadAllText("input.log", gzip));
            Assert.Equal(input, ReadAllText("input_0000.log", gzip));
            AssertFileCount(2);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WritesSingleFileIfDesiredSizeIsLarger(bool gzip)
        {
            var input = $"aaaa{BR}bbbb{BR}cccc{BR}";
            WriteFile(input, gzip);

            var exitCode = Program.Main(new[] { FilePath, (FileSize + 1).ToString() });

            Assert.Equal(0, exitCode);
            Assert.Equal(input, ReadAllText("input.log", gzip));
            Assert.Equal(input, ReadAllText("input_0000.log", gzip));
            AssertFileCount(2);
        }

        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(1, 1, 1)]
        [InlineData(1, 2, 1)]
        [InlineData(2, 1, 2)]
        [InlineData(100, 33, 4)]
        [InlineData(100, 34, 3)]
        [InlineData(1_024, 1_023, 2)]
        [InlineData(1_023, 1_024, 1)]
        [InlineData(long.MaxValue, 1, long.MaxValue)]
        [InlineData(1, long.MaxValue, 1)]
        [InlineData(long.MaxValue, long.MaxValue - 1, 2)]
        [InlineData(long.MaxValue - 1, long.MaxValue, 1)]
        [InlineData(((long)int.MaxValue) * 999_999_999 + 1, int.MaxValue, 1_000_000_000)]
        public void SelectsCorrectFileCount(long size, long desiredSize, long expected)
        {
            Assert.Equal(expected, Program.GetFileCount(size, desiredSize));
        }

        private TestDirectory TestDirectory { get; }
        private string FileName { get; }
        private string FilePath => Path(FileName);
        private long FileSize => new FileInfo(FilePath).Length;

        private void WriteFile(string contents, bool gzip = false)
        {
            using (var fileStream = File.OpenWrite(FilePath))
            {
                Stream writeStream = fileStream;
                if (gzip)
                {
                    writeStream = new GZipStream(fileStream, CompressionMode.Compress);
                }

                using (writeStream)
                using (var writer = new StreamWriter(writeStream))
                {
                    writer.Write(contents);
                }
            }
        }

        private string ReadAllText(string fileName, bool gzip = false)
        {
            using (var fileStream = File.OpenRead(Path(fileName)))
            {
                Stream readStream = fileStream;
                if (gzip)
                {
                    readStream = new GZipStream(fileStream, CompressionMode.Decompress);
                }

                using (readStream)
                using (var reader = new StreamReader(readStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private void AssertFileCount(int count)
        {
            Assert.Equal(count, new DirectoryInfo(TestDirectory).EnumerateFiles().Count());
        }

        private string Path(string fileName)
        {
            return System.IO.Path.Combine(TestDirectory, fileName);
        }

        public void Dispose()
        {
            TestDirectory.Dispose();
        }
    }
}
