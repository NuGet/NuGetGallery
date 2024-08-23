// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public abstract class BaseTempFileFacts
    {
        [Fact]
        public void ProvidesFullFilePath()
        {
            WithTempFile(tempFile =>
            {
                Assert.True(PathUtility.IsFilePathAbsolute(tempFile.FullName));
            });
        }

        [Fact]
        public void CreatesEmptyFile()
        {
            WithTempFile(tempFile =>
            {
                Assert.True(File.Exists(tempFile.FullName));
                var fi = new FileInfo(tempFile.FullName);
                Assert.Equal(0, fi.Length);
            });
        }

        [Fact]
        public void DeletesFileOnDispose()
        {
            string filename = null;
            WithTempFile(tempFile => filename = tempFile.FullName);
            Assert.False(File.Exists(filename));
        }

        [Fact]
        public void CreatedFileIsWritable()
        {
            var ex = Record.Exception(() => WithTempFile(tempFile => File.WriteAllText(tempFile.FullName, "some content")));
            Assert.Null(ex);
        }

        [Fact]
        public void DoesNotThrowIfFileDoesNotExistOnDispose()
        {
            var ex = Record.Exception(() => WithTempFile(tempFile => File.Delete(tempFile.FullName)));
            Assert.Null(ex);
        }

        protected abstract void WithTempFile(Action<ITempFile> action);
    }

    public class TheTempFileConstructor : BaseTempFileFacts
    {
        protected override void WithTempFile(Action<ITempFile> action)
        {
            using (var tempFile = new TempFile())
            {
                action(tempFile);
            }
        }
    }

    public class TheTempFileCreateMethod : BaseTempFileFacts
    {
        private const string DirectoryName = "TheTempFileCreateMethod";

        protected override void WithTempFile(Action<ITempFile> action)
        {
            WithTempFile(DirectoryName, action);
        }

        protected void WithTempFile(string directoryName, Action<ITempFile> action)
        {
            using (var tempFile = TempFile.Create(directoryName))
            {
                action(tempFile);
            }
        }

        [Fact]
        public void UsesDirectoryName()
        {
            var expectedDirectory = Path.Combine(Path.GetTempPath(), DirectoryName);
            WithTempFile(tempFile =>
            {
                var fi = new FileInfo(tempFile.FullName);
                Assert.Equal(expectedDirectory, fi.Directory.FullName);
            });
        }

        [Fact]
        public void OkWithNestingDirectories()
        {
            const string nestedDirectory = @"TempFileTest\Nested";
            var expectedDirectory = Path.Combine(Path.GetTempPath(), nestedDirectory);
            WithTempFile(nestedDirectory, tempFile =>
            {
                var fi = new FileInfo(tempFile.FullName);
                Assert.Equal(expectedDirectory, fi.Directory.FullName);
            });
        }
    }
}
