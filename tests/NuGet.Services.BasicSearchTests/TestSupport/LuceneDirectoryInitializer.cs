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

        public Lucene.Net.Store.Directory GetInitializedDirectory(IEnumerable<PackageVersion> packages)
        {
            string baseLuceneDirectory = GetBaseLuceneDirectory();
            string directory = null;
            if (baseLuceneDirectory != null)
            {
                directory = Path.Combine(baseLuceneDirectory, Guid.NewGuid().ToString());
            }
            
            return CreateLuceneIndex(packages, directory);
        }

        private string GetBaseLuceneDirectory()
        {
            if (_settings.BaseLuceneDirectory == null)
            {
                return null;
            }

            if (!Path.IsPathRooted(_settings.BaseLuceneDirectory))
            {
                return Path.Combine(System.IO.Directory.GetCurrentDirectory(), _settings.BaseLuceneDirectory);
            }

            return _settings.BaseLuceneDirectory;
        }

        private Lucene.Net.Store.Directory CreateLuceneIndex(IEnumerable<PackageVersion> packages, string luceneDirectory)
        {
            Lucene.Net.Store.Directory directory;
            if (luceneDirectory != null)
            {
                var directoryInfo = new DirectoryInfo(luceneDirectory);
                directoryInfo.Create();
                directory = new SimpleFSDirectory(directoryInfo);
            }
            else
            {
                directory = new RAMDirectoryWrapper();
            }
            
            using (var indexWriter = DocumentCreator.CreateIndexWriter(directory, true))
            {
                foreach (var version in packages)
                {
                    var metadata = GetPackageMetadata(version);
                    var document = DocumentCreator.CreateDocument(metadata);
                    indexWriter.AddDocument(document);
                }

                indexWriter.Commit();
            }

            return directory;
        }

        private IDictionary<string, string> GetPackageMetadata(PackageVersion version)
        {
            var path = _nupkgDownloader.GetPackagePath(version);

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var errors = new List<string>();
                var metadata = NupkgPackageMetadataExtraction.MakePackageMetadata(fileStream, errors);

                if (errors.Any())
                {
                    throw new InvalidOperationException(
                        $"NuGet package for '{version.Id}' (version '{version.Version}') could not be read for metadata. " +
                        $"Errors: {string.Join(", ", errors)}");
                }

                metadata["listed"] = version.Listed.ToString().ToLowerInvariant();

                return metadata;
            }
        }
    }
}