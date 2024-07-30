// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests.TempFiles
{
    public class DeleteOnCloseReadOnlyTempFileFacts
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
        public void DeletesFileOnDispose()
        {
            string filename = null;
            WithTempFile(tempFile => filename = tempFile.FullName);
            Assert.False(File.Exists(filename));
        }

        [Fact]
        public void AllowsReadingAll()
        {
            const string content = "SomeContent";

            string readContent = null;
            WithTempFile(content, tempFile =>
            {
                using (var sr = new StreamReader(tempFile.ReadStream))
                {
                    readContent = sr.ReadToEnd();
                }
            });

            Assert.Equal(content, readContent);
        }

        [Fact]
        public async Task ReadToEndReadsEverything()
        {
            const string content = "SomeContent";

            string readContent1 = null;
            string readContent2 = null;

            await WithTempFile(content, async tempFile =>
            {
                using (var sr = new StreamReader(tempFile.ReadStream))
                {
                    readContent1 = sr.ReadToEnd();
                    tempFile.ReadStream.Seek(0, SeekOrigin.Begin);
                    readContent2 = await tempFile.ReadToEndAsync();
                }
            });

            Assert.Equal(content, readContent1);
            Assert.Equal(content, readContent2);
        }

        private void WithTempFile(Action<ITempReadOnlyFile> action)
        {
            WithTempFile(TempFileActionWrapper(action), null).GetAwaiter().GetResult();
        }

        private void WithTempFile(string content, Action<ITempReadOnlyFile> action)
        {
            WithTempFile(TempFileActionWrapper(action), content).GetAwaiter().GetResult();
        }

        private async Task WithTempFile(string content, Func<ITempReadOnlyFile, Task> action)
        {
            await WithTempFile(action, content);
        }

        private static Func<ITempReadOnlyFile, Task> TempFileActionWrapper(Action<ITempReadOnlyFile> action)
            => tempFile =>
                {
                    action(tempFile);
                    return Task.CompletedTask;
                };

        private async Task WithTempFile(Func<ITempReadOnlyFile, Task> action, string content)
        {
            var tempFileName = Path.GetTempFileName();
            if (content != null)
            {
                File.WriteAllText(tempFileName, content);
            }
            using (var tempFile = new DeleteOnCloseReadOnlyTempFile(tempFileName))
            {
                await action(tempFile);
            }
        }
    }
}
