// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class FileStreamUtilityFacts
    {
        public class GetTemporaryFile
        {
            private const string FileContent = "Hello, world.";

            [Fact]
            public void ReturnsFileStreamThatDeletesOnDispose()
            {
                // Arrange
                string fileName;
                using (var stream = FileStreamUtility.GetTemporaryFile())
                using (var writer = new StreamWriter(stream))
                {
                    fileName = stream.Name;
                    writer.WriteLine(FileContent);
                    writer.Flush();

                    // Act & Assert
                    Assert.True(File.Exists(fileName), "The file should still exist.");
                }

                Assert.False(File.Exists(fileName), "The file should no longer exist.");
            }

            [Fact]
            public void CanReadAndWriteStream()
            {
                // Arrange
                using (var stream = FileStreamUtility.GetTemporaryFile())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream))
                {
                    // Act & Assert
                    writer.Write(FileContent);
                    writer.Flush();
                    stream.Position = 0;
                    var actualContent = reader.ReadToEnd();
                    Assert.Equal(FileContent, actualContent);
                }
            }
        }
    }
}
