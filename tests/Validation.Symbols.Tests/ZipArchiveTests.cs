// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Symbols.Core;
using Moq;
using Xunit;


namespace Validation.Symbols.Tests
{
    public class ZipArchiveTests
    {
        public class TheRemoveExtensionMethod
        {
            [Fact]
            public void NullChecks()
            {
                IEnumerable<string> input = null;
                // Act + Assert
                Assert.Null(ZipArchiveService.RemoveExtension(input));
            }

            [Fact]
            public void TheFileExtensionsAreRemoved()
            {
                IEnumerable<string> input = new string[]{ "foo1.exe", "foo2.pdb", "foo3.dll", @"a\foo1.exe", @"a\foo2.pdb"};
                string[] expected = new string[] { "foo1", "foo2", "foo3", @"a\foo1", @"a\foo2" };
                
                // Act
                var result = ZipArchiveService.RemoveExtension(input);

                Assert.Equal(expected.Length, result.Count());
                foreach(string s in expected)
                {
                    Assert.Contains(s, result);
                }
            }
        }

        public class TheReadFilesFromZipStreamMethod
        {
            [Fact]
            public void NullCheck()
            {
                IReadOnlyCollection<ZipArchiveEntry> input = null;
                // Act + Assert
                Assert.Throws<ArgumentNullException>( ()=>ZipArchiveService.ReadFilesFromZipStream(input, ".exe"));
            }

            [Fact]
            public void TheCorrectExtensionsAreFiltered()
            {
                // Arrange
                IReadOnlyCollection<ZipArchiveEntry> input = CreateTestZipEntries(new string[] { "foo.pdb", "bar.txt", "foobar.dll", @"a\bar.pdb"});
                string[] expected = new string[] { "foo.pdb", @"a\bar.pdb" };

                // Act
                var result =  ZipArchiveService.ReadFilesFromZipStream(input, ".pdb");

                // Assert 
                Assert.Equal(expected.Length, result.Count());
                foreach (string s in expected)
                {
                    Assert.Contains(s, result);
                }
            }
        }

        public class TheExtractMethod
        {
            [Fact]
            public void NullCheck()
            {
                // Arrange
                IReadOnlyCollection<ZipArchiveEntry> input = new ReadOnlyCollection<ZipArchiveEntry>(new List<ZipArchiveEntry>());
                Mock<ILogger<ZipArchiveService>> zipServiceLogger = new Mock<ILogger<ZipArchiveService>>();
                var service = new ZipArchiveService(zipServiceLogger.Object);

                // Act + Assert
                Assert.Throws<ArgumentNullException>(() => service.Extract(null, "Dir1", new List<string> {".pdb"}));
                Assert.Throws<ArgumentNullException>(() => ZipArchiveService.ReadFilesFromZipStream(input, null));
            }

            [Fact]
            public void ExtractReturnsTheListOfFileEntriesNoFilter()
            {
                // Arrange
                IReadOnlyCollection<ZipArchiveEntry> input = CreateTestZipEntries(new string[] { "foo.pdb", "bar.txt", "foobar.dll", @"a\bar.pdb" });
                string[] expected = new string[] { "foo.pdb", "bar.txt", "foobar.dll", @"a\bar.pdb" };
                var service = new TestZipArchiveService();

                // Act
                var result = service.Extract(input, "Dir1");

                // Assert 
                Assert.Equal(expected.Length, result.Count());
                foreach (string s in expected)
                {
                    Assert.Contains(s, result);
                }
            }

            [Fact]
            public void ExtractReturnsTheListOfFileEntriesWithFilter()
            {
                // Arrange
                IReadOnlyCollection<ZipArchiveEntry> input = CreateTestZipEntries(new string[] { "foo.dll", "bar.exe", "foobar.dll", @"a\bar.dll" });
                var symbolFilter = new HashSet<string>() { "foo.pdb", @"a\bar.pdb" };

                // Extract only the file with the same path entry as the input but ignoring the extension
                string[] expected = new string[] { "foo.dll", @"a\bar.dll" };

                var service = new TestZipArchiveService();

                // Act
                var result = service.Extract(input, "Dir1", null, symbolFilter);

                // Assert 
                Assert.Equal(expected.Length, result.Count());
                Assert.True(service.OnExtractInvoked);
                foreach (string s in expected)
                {
                    Assert.Contains(s, result);
                }
            }

            [Fact]
            public void FailsWithDirectoryTraversal()
            {
                // Arrange
                IReadOnlyCollection<ZipArchiveEntry> input = CreateTestZipEntries(new string[] { "../foo.pdb" });
                var service = new TestZipArchiveService();

                // Act & Assert
                Assert.Throws<InvalidDataException>(() => service.Extract(input, "Dir1").ToList());
            }
        }

        public static IReadOnlyCollection<ZipArchiveEntry> CreateTestZipEntries(string[] files)
        {
            List<ZipArchiveEntry> data = new List<ZipArchiveEntry>();
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var f in files)
                    {
                        data.Add(archive.CreateEntry(f));
                    }
                }

                return new ReadOnlyCollection<ZipArchiveEntry>(data);
            }
        }

        private class TestZipArchiveService : ZipArchiveService
        {
            public TestZipArchiveService( ) : base(new Mock<ILogger<ZipArchiveService>>().Object)
            {
            }

            public bool OnExtractInvoked = false;

            public override void OnExtract(ZipArchiveEntry entry, string destinationPath, string targetDirectory)
            {
                OnExtractInvoked = true;
            }
        }
    }
}
