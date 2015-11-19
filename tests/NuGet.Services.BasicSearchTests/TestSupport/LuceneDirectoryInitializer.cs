// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Store;
using NuGet.Indexing;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class LuceneDirectoryInitializer
    {
        private readonly TestSettings _settings;
        private readonly INupkgDownloader _nupkgDownloader;

        public LuceneDirectoryInitializer(TestSettings settings, INupkgDownloader nupkgDownloader)
        {
            _settings = settings;
            _nupkgDownloader = nupkgDownloader;
        }

        public string GetInitializedDirectory(IEnumerable<PackageVersion> packages)
        {
            string directory = Path.Combine(GetBaseLuceneDirectory(), Guid.NewGuid().ToString());
            CreateLuceneIndex(packages, directory);
            return directory;
        }

        private string GetBaseLuceneDirectory()
        {
            if (_settings.BaseLuceneDirectory == null)
            {
                return System.IO.Directory.GetCurrentDirectory();
            }

            if (!Path.IsPathRooted(_settings.BaseLuceneDirectory))
            {
                return Path.Combine(System.IO.Directory.GetCurrentDirectory(), _settings.BaseLuceneDirectory);
            }

            return _settings.BaseLuceneDirectory;
        }

        private void CreateLuceneIndex(IEnumerable<PackageVersion> packages, string directory)
        {
            var directoryInfo = new DirectoryInfo(directory);
            directoryInfo.Create();

            using (var indexWriter = DocumentCreator.CreateIndexWriter(new SimpleFSDirectory(directoryInfo), true))
            {
                foreach (var version in packages)
                {
                    var metadata = GetPackageMetadata(version);
                    var document = DocumentCreator.CreateDocument(metadata);
                    indexWriter.AddDocument(document);
                }

                indexWriter.Commit();
            }
        }

        private IDictionary<string, string> GetPackageMetadata(PackageVersion version)
        {
            var path = _nupkgDownloader.GetPackagePath(version);

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var errors = new List<string>();
                var metadata = PackageMetadataExtraction.MakePackageMetadata(fileStream, errors);

                if (errors.Any())
                {
                    throw new InvalidOperationException(
                        $"NuGet package for '{version.Id}' (version '{version.Version}') could not be read for metadata. " +
                        $"Errors: {string.Join(", ", errors)}");
                }

                return metadata;
            }
        }
    }
}