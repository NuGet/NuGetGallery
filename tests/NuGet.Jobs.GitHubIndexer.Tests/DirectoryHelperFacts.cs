// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class DirectoryHelperFacts
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly string _testRoot;

        public DirectoryHelperFacts()
        {
            _mockLogger = new Mock<ILogger>();
            _testRoot = Path.Combine(Path.GetTempPath(), "DirectoryHelperTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
        }

        private string CreateTestDir(string name = "testdir")
        {
            var path = Path.Combine(_testRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        public class NonExistentDirectory : DirectoryHelperFacts
        {
            [Fact]
            public void DoesNotThrow()
            {
                var path = Path.Combine(_testRoot, "does_not_exist");

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class EmptyDirectory : DirectoryHelperFacts
        {
            [Fact]
            public void DeletesSuccessfully()
            {
                var path = CreateTestDir();

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class DirectoryWithFiles : DirectoryHelperFacts
        {
            [Fact]
            public void DeletesDirectoryAndContents()
            {
                var path = CreateTestDir();
                File.WriteAllText(Path.Combine(path, "file1.txt"), "content1");
                File.WriteAllText(Path.Combine(path, "file2.txt"), "content2");

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class NestedDirectories : DirectoryHelperFacts
        {
            [Fact]
            public void DeletesDeeplyNestedStructure()
            {
                var path = CreateTestDir();
                var nested = Path.Combine(path, "level1", "level2", "level3");
                Directory.CreateDirectory(nested);
                File.WriteAllText(Path.Combine(nested, "deep.txt"), "deep content");
                File.WriteAllText(Path.Combine(path, "level1", "shallow.txt"), "shallow content");

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class ReadOnlyFiles : DirectoryHelperFacts
        {
            [Fact]
            public void DeletesDirectoryWithReadOnlyFiles()
            {
                var path = CreateTestDir();
                var filePath = Path.Combine(path, "readonly.txt");
                File.WriteAllText(filePath, "content");
                File.SetAttributes(filePath, FileAttributes.ReadOnly);

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }

            [Fact]
            public void DeletesNestedReadOnlyFiles()
            {
                var path = CreateTestDir();
                var subDir = Path.Combine(path, "subdir");
                Directory.CreateDirectory(subDir);

                var file1 = Path.Combine(subDir, "readonly1.txt");
                var file2 = Path.Combine(subDir, "readonly2.txt");
                File.WriteAllText(file1, "content1");
                File.WriteAllText(file2, "content2");
                File.SetAttributes(file1, FileAttributes.ReadOnly);
                File.SetAttributes(file2, FileAttributes.ReadOnly);

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class ReservedDeviceNames : DirectoryHelperFacts
        {
            [Theory]
            [InlineData("AUX")]
            [InlineData("CON")]
            [InlineData("PRN")]
            [InlineData("NUL")]
            public void DeletesDirectoryWithReservedName(string reservedName)
            {
                var reservedPath = Path.Combine(_testRoot, reservedName);
                var uncPath = @"\\?\" + Path.GetFullPath(reservedPath);

                var createProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mkdir \"{uncPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                createProcess.WaitForExit();

                if (createProcess.ExitCode != 0)
                {
                    Cleanup();
                    return;
                }

                DirectoryHelper.DeleteDirectoryWithRetries(reservedPath, _mockLogger.Object);

                Assert.False(Directory.Exists(uncPath));
                Cleanup();
            }
        }

        public class MaxRetriesParameter : DirectoryHelperFacts
        {
            [Fact]
            public void RespectsCustomMaxRetries()
            {
                var path = CreateTestDir();
                File.WriteAllText(Path.Combine(path, "file.txt"), "content");

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object, maxRetries: 1);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }

        public class UnicodeDirectoryNames : DirectoryHelperFacts
        {
            [Fact]
            public void DeletesDirectoryWithUnicodeName()
            {
                var path = CreateTestDir("テスト_目录_тест");
                File.WriteAllText(Path.Combine(path, "file.txt"), "content");

                DirectoryHelper.DeleteDirectoryWithRetries(path, _mockLogger.Object);

                Assert.False(Directory.Exists(path));
                Cleanup();
            }
        }
    }
}
