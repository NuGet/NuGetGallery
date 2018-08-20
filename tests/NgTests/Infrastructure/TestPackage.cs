// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NuGet.Versioning;

namespace NgTests.Infrastructure
{
    internal sealed class TestPackage : IDisposable
    {
        private bool _isDisposed;

        internal string Id { get; }
        internal NuGetVersion Version { get; }
        internal string Author { get; }
        internal string Description { get; }
        internal Stream Stream { get; private set; }

        internal TestPackage(string id, NuGetVersion version, string author, string description, Stream stream)
        {
            Id = id;
            Version = version;
            Author = author;
            Description = description;
            Stream = stream;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Stream.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        internal static TestPackage Create(Random random)
        {
            var id = TestUtility.CreateRandomAlphanumericString(random);
            var version = CreateRandomVersion(random);
            var author = TestUtility.CreateRandomAlphanumericString(random);
            var description = TestUtility.CreateRandomAlphanumericString(random);
            var nuspec = CreateNuspec(id, version, author, description);

            using (var rng = RandomNumberGenerator.Create())
            {
                var stream = CreatePackageStream(id, nuspec, rng, random);

                return new TestPackage(id, version, author, description, stream);
            }
        }

        private static string CreateNuspec(string id, NuGetVersion version, string author, string description)
        {
            return $@"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>{id}</id>
    <version>{version.ToNormalizedString()}</version>
    <authors>{author}</authors>
    <description>{description}</description>
  </metadata>
</package>";
        }

        private static MemoryStream CreatePackageStream(string id, string nuspec, RandomNumberGenerator rng, Random random)
        {
            var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry($"{id}.nuspec");

                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write(nuspec);
                }

                // The max value is arbitrary.  We just need a little variation in package entries.
                var entryCount = random.Next(minValue: 1, maxValue: 3);

                for (var i = 0; i < entryCount; ++i)
                {
                    entry = zip.CreateEntry($"file{i}.bin");

                    using (var entryStream = entry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        var byteCount = random.Next(1, 10);
                        var bytes = new byte[byteCount];

                        rng.GetNonZeroBytes(bytes);

                        writer.Write(bytes);
                    }
                }
            }

            return stream;
        }

        private static NuGetVersion CreateRandomVersion(Random random)
        {
            var major = random.Next(minValue: 1, maxValue: 10);
            var minor = random.Next(minValue: 1, maxValue: 10);
            var build = random.Next(minValue: 1, maxValue: 10);

            return new NuGetVersion(major, minor, build);
        }
    }
}