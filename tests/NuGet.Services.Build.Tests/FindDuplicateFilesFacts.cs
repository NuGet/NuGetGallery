// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Build.Tests
{
    public class FindDuplicateFilesFacts
    {
        public class Execute : Facts
        {
            public Execute(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void IgnoresMissingFiles()
            {
                var doesNotExist = AddFile("does-not-exist.txt");

                var success = Target.Execute();

                Assert.True(success);
                Assert.Empty(Target.UniqueFiles);
                Assert.Empty(Target.DuplicateFiles);
                VerifySetMetadata(doesNotExist, Times.Never);
            }

            [Fact]
            public void IgnoresDuplicateFiles()
            {
                var a1 = AddAndWriteFile("a.txt", string.Empty);
                var a2 = AddAndWriteFile("a.txt", string.Empty);

                var success = Target.Execute();

                Assert.True(success);
                Assert.Equal(new[] { a1.Object }, Target.UniqueFiles);
                Assert.Empty(Target.DuplicateFiles);
                VerifySetMetadata(a1, Times.Never);
                VerifySetMetadata(a1, Times.Never);
            }

            [Fact]
            public void DetectsFilesWithDifferentFileSizes()
            {
                var a = AddAndWriteFile("a.txt", string.Empty);
                var b = AddAndWriteFile("b.txt", "b");

                var success = Target.Execute();

                Assert.True(success);
                Assert.Equal(new[] { a.Object, b.Object }, Target.UniqueFiles);
                Assert.Empty(Target.DuplicateFiles);
                VerifySetMetadata(a, Times.Never);
                VerifySetMetadata(b, Times.Never);
            }

            [Theory]
            [MemberData(nameof(LengthData))]
            public void DetectsFilesWithDifferentLeadingBytes(int commonPrefixLength)
            {
                var prefix = new string('_', commonPrefixLength);
                var a = AddAndWriteFile("a.txt", prefix + "a");
                var b = AddAndWriteFile("b.txt", prefix + "b");

                var success = Target.Execute();

                Assert.True(success);
                Assert.Equal(new[] { a.Object, b.Object }, Target.UniqueFiles);
                Assert.Empty(Target.DuplicateFiles);
                VerifySetMetadata(a, Times.Never);
                VerifySetMetadata(b, Times.Never);
            }

            [Theory]
            [MemberData(nameof(LengthData))]
            public void DetectsDuplicateFiles(int length)
            {
                var content = new string('_', length);
                var a = AddAndWriteFile("a.txt", content);
                var b = AddAndWriteFile("b.txt", content);

                var success = Target.Execute();

                Assert.True(success);
                Assert.Equal(new[] { a.Object }, Target.UniqueFiles);
                Assert.Equal(new[] { b.Object }, Target.DuplicateFiles);
                VerifySetMetadata(a, Times.Never);
                VerifySetDuplicateOf(b, a.Object.ItemSpec);
            }

            [Theory]
            [MemberData(nameof(NonZeroLengthData))]
            public void DetectsMultipleDuplicates(int length)
            {
                var a = AddAndWriteFile("a.txt", new string('1', length));
                var b = AddAndWriteFile("b.txt", new string('2', length));
                var c = AddAndWriteFile("c.txt", new string('2', length));
                var d = AddAndWriteFile("d.txt", new string('2', length));
                var e = AddAndWriteFile("e.txt", new string('2', length));
                var f = AddAndWriteFile("f.txt", new string('3', length));
                var g = AddAndWriteFile("g.txt", new string('3', length));

                var success = Target.Execute();

                Assert.True(success);
                Assert.Equal(new[] { a.Object, b.Object, f.Object }, Target.UniqueFiles);
                Assert.Equal(new[] { c.Object, d.Object, e.Object, g.Object }, Target.DuplicateFiles);
                VerifySetMetadata(a, Times.Never);
                VerifySetMetadata(b, Times.Never);
                VerifySetMetadata(f, Times.Never);
                VerifySetDuplicateOf(c, b.Object.ItemSpec);
                VerifySetDuplicateOf(d, b.Object.ItemSpec);
                VerifySetDuplicateOf(e, b.Object.ItemSpec);
                VerifySetDuplicateOf(g, f.Object.ItemSpec);
            }

            public static int[] Lengths => new[] { 0, 1, 64, 512, 1023, 1024, 1025, 2048, 32768 };
            public static IEnumerable<object[]> NonZeroLengthData => Lengths.Where(x => x != 0).Select(x => new object[] { x });
            public static IEnumerable<object[]> LengthData => Lengths.Select(x => new object[] { x });
        }

        public abstract class Facts : IDisposable
        {
            private readonly string _directory;

            public FindDuplicateFiles Target { get; }
            public Mock<IBuildEngine> BuildEngine { get; }
            public List<ITaskItem> Files { get; }

            public Facts(ITestOutputHelper output)
            {
                _directory = Path.Combine(Path.GetTempPath(), "NuGet.Services.Build.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);

                BuildEngine = new Mock<IBuildEngine>();
                Files = new List<ITaskItem>();

                BuildEngine
                    .Setup(x => x.LogCustomEvent(It.IsAny<CustomBuildEventArgs>()))
                    .Callback<CustomBuildEventArgs>(e => output.WriteLine($"[CUSTOM] {e.SenderName}: {e.Message}"));
                BuildEngine
                    .Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                    .Callback<BuildErrorEventArgs>(e => output.WriteLine($"[ERROR] {e.SenderName}: {e.Message}"));
                BuildEngine
                    .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                    .Callback<BuildMessageEventArgs>(e => output.WriteLine($"[MESSAGE] {e.SenderName}: {e.Message}"));
                BuildEngine
                    .Setup(x => x.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
                    .Callback<BuildWarningEventArgs>(e => output.WriteLine($"[WARNING] {e.SenderName}: {e.Message}"));

                Target = new FindDuplicateFiles();
                Target.BuildEngine = BuildEngine.Object;
                Target.Files = Files.ToArray();
            }

            public void VerifySetMetadata(Mock<ITaskItem> item, Func<Times> times)
            {
                item.Verify(x => x.SetMetadata(It.IsAny<string>(), It.IsAny<string>()), times);
            }

            public void VerifySetDuplicateOf(Mock<ITaskItem> item, string duplicateOf)
            {
                VerifySetMetadata(item, Times.Once);
                item.Verify(x => x.SetMetadata("DuplicateOf", duplicateOf), Times.Once);
            }

            public Mock<ITaskItem> AddAndWriteFile(string path, string content)
            {
                var item = AddFile(path);
                File.WriteAllText(item.Object.ItemSpec, content, Encoding.UTF8);
                return item;
            }

            public Mock<ITaskItem> AddFile(string path)
            {
                var fullPath = Path.Combine(_directory, path);
                var item = new Mock<ITaskItem>();
                item.Setup(x => x.ItemSpec).Returns(fullPath);
                item.Setup(x => x.GetMetadata("FullPath")).Returns(fullPath);

                Files.Add(item.Object);
                Target.Files = Files.ToArray();

                return item;
            }

            public void Dispose()
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
