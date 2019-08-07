﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Index;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure.Lucene;
using WebBackgrounder;

namespace NuGetGallery
{
    public class LuceneIndexingService : IIndexingService, IIndexingJobFactory
    {
        private static readonly object IndexWriterLock = new object();

        private static readonly TimeSpan IndexRecreateInterval = TimeSpan.FromHours(3);

        private static ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter> WriterCache =
            new ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter>();

        private readonly Lucene.Net.Store.Directory _directory;
        private IndexWriter _indexWriter;
        private IEntityRepository<Package> _packageRepository;
        private readonly Func<bool> _getShouldAutoUpdate;
        private readonly ILuceneDocumentFactory _luceneDocumentFactory;

        private IDiagnosticsSource Trace { get; set; }

        public string IndexPath
        {
            get { return LuceneCommon.GetDirectoryLocation(); }
        }

        public bool IsLocal
        {
            get { return true; }
        }

        public LuceneIndexingService(
            IEntityRepository<Package> packageSource,
            Lucene.Net.Store.Directory directory,
            IDiagnosticsService diagnostics,
            IAppConfiguration config,
            ILuceneDocumentFactory luceneDocumentFactory)
        {
            _packageRepository = packageSource;
            _directory = directory;
            _getShouldAutoUpdate = config == null ? new Func<bool>(() => true) : new Func<bool>(() => config.AutoUpdateSearchIndex);
            _luceneDocumentFactory = luceneDocumentFactory ?? throw new ArgumentNullException(nameof(luceneDocumentFactory));
            Trace = diagnostics.SafeGetSource("LuceneIndexingService");
        }

        public void UpdateIndex()
        {
            if (_getShouldAutoUpdate())
            {
                UpdateIndex(forceRefresh: false);
            }
        }

        public void UpdateIndex(bool forceRefresh)
        {
            // Always do it if we're asked to "force" a refresh (i.e. manually triggered)
            // Otherwise, no-op unless we're supporting background search indexing.
            if (forceRefresh || _getShouldAutoUpdate())
            {
                DateTime? lastWriteTime = GetLastWriteTime().Result;

                if ((lastWriteTime == null) || IndexRequiresRefresh() || forceRefresh)
                {
                    EnsureIndexWriter(creatingIndex: true);
                    _indexWriter.DeleteAll();
                    _indexWriter.Commit();

                    // Reset the lastWriteTime to null. This will allow us to get a fresh copy of all the latest \ latest successful packages
                    lastWriteTime = null;

                    // Set the index create time to now. This would tell us when we last rebuilt the index.
                    UpdateIndexRefreshTime();
                }

                var packages = GetPackages(lastWriteTime);
                if (packages.Count > 0)
                {
                    EnsureIndexWriter(creatingIndex: lastWriteTime == null);
                    AddPackagesCore(packages, creatingIndex: lastWriteTime == null);
                }

                UpdateLastWriteTime();
            }
        }

        public void UpdatePackage(Package package)
        {
            if (_getShouldAutoUpdate())
            {
                var packageRegistrationKey = package.PackageRegistrationKey;
                var updateTerm = new Term("PackageRegistrationKey", packageRegistrationKey.ToString(CultureInfo.InvariantCulture));

                if (!package.IsLatest || !package.IsLatestStable)
                {
                    // Someone passed us in a version which was e.g. just unlisted? Or just not the latest version which is what we want to index. Doesn't really matter. We'll find one to index.
                    package = _packageRepository.GetAll()
                        .Where(p => (p.IsLatest || p.IsLatestStable)
                                    && p.PackageRegistrationKey == packageRegistrationKey
                                    && p.PackageStatusKey == PackageStatus.Available)
                        .Include(p => p.PackageRegistration)
                        .Include(p => p.PackageRegistration.Owners)
                        .Include(p => p.SupportedFrameworks)
                        .FirstOrDefault();
                }

                // Just update the provided package
                using (Trace.Activity(String.Format(CultureInfo.CurrentCulture, "Updating Document: {0}", updateTerm.ToString())))
                {
                    EnsureIndexWriter(creatingIndex: false);
                    if (package != null)
                    {
                        Trace.Information(String.Format(CultureInfo.CurrentCulture, "Updating Lucene Index for: {0} {1} [PackageKey:{2}]", package.PackageRegistration.Id, package.Version, package.Key));
                        _indexWriter.UpdateDocument(updateTerm, _luceneDocumentFactory.Create(package));
                    }
                    else
                    {
                        Trace.Information(String.Format(CultureInfo.CurrentCulture, "Deleting Document: {0}", updateTerm.ToString()));
                        _indexWriter.DeleteDocuments(updateTerm);
                    }
                    _indexWriter.Commit();
                }
            }
        }

        private List<Package> GetPackages(DateTime? lastIndexTime)
        {
            IQueryable<Package> set = _packageRepository
                .GetAll()
                .Where(p => p.PackageStatusKey == PackageStatus.Available);

            if (lastIndexTime.HasValue)
            {
                // Retrieve the Latest, LatestStable, LatestSemVer2 and LatestStableSemVer2 version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                set = set.Where(
                    p => (p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2) &&
                        p.PackageRegistration.Packages.Any(p2 => p2.LastUpdated > lastIndexTime));
            }
            else
            {
                set = set.Where(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2);  // which implies that p.IsListed by the way!
            }

            var list = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks)
                .ToList();

            return list;
        }

        public void AddPackages(IList<Package> packages, bool creatingIndex)
        {
            if (_getShouldAutoUpdate())
            {
                AddPackagesCore(packages, creatingIndex);
            }
        }

        private void AddPackagesCore(IList<Package> packages, bool creatingIndex)
        {
            if (!creatingIndex)
            {
                // If this is not the first time we're creating the index, clear any package registrations for packages we are going to updating.
                var packagesToDelete = from packageRegistrationKey in packages.Select(p => p.PackageRegistrationKey).Distinct()
                                       select new Term("PackageRegistrationKey", packageRegistrationKey.ToString(CultureInfo.InvariantCulture));
                _indexWriter.DeleteDocuments(packagesToDelete.ToArray());
            }

            // As per http://stackoverflow.com/a/3894582. The IndexWriter is CPU bound, so we can try and write multiple packages in parallel.
            // The IndexWriter is thread safe and is primarily CPU-bound.
            Parallel.ForEach(packages, AddPackage);

            _indexWriter.Commit();
        }

        public virtual Task<DateTime?> GetLastWriteTime()
        {
            var metadataPath = LuceneCommon.GetIndexMetadataPath();
            if (!File.Exists(metadataPath))
            {
                return Task.FromResult<DateTime?>(null);
            }
            return Task.FromResult<DateTime?>(File.GetLastWriteTimeUtc(metadataPath));
        }

        private void AddPackage(Package package)
        {
            _indexWriter.AddDocument(_luceneDocumentFactory.Create(package));
        }

        protected void EnsureIndexWriter(bool creatingIndex)
        {
            if (_indexWriter == null)
            {
                if (WriterCache.TryGetValue(_directory, out _indexWriter))
                {
                    Debug.Assert(_indexWriter != null);
                    return;
                }

                lock (IndexWriterLock)
                {
                    if (WriterCache.TryGetValue(_directory, out _indexWriter))
                    {
                        Debug.Assert(_indexWriter != null);
                        return;
                    }

                    EnsureIndexWriterCore(creatingIndex);
                }
            }
        }

        private void EnsureIndexWriterCore(bool creatingIndex)
        {
            var analyzer = new PerFieldAnalyzer();
            _indexWriter = new IndexWriter(_directory, analyzer, create: creatingIndex, mfl: IndexWriter.MaxFieldLength.UNLIMITED);

            // Should always be add, due to locking
            var got = WriterCache.GetOrAdd(_directory, _indexWriter);
            Debug.Assert(got == _indexWriter);
        }

        protected internal static bool IndexRequiresRefresh()
        {
            var metadataPath = LuceneCommon.GetIndexMetadataPath();
            if (File.Exists(metadataPath))
            {
                var creationTime = File.GetCreationTimeUtc(metadataPath);
                return (DateTime.UtcNow - creationTime) > IndexRecreateInterval;
            }

            // If we've never created the index, it needs to be refreshed.
            return true;
        }

        protected internal virtual void UpdateLastWriteTime()
        {
            var metadataPath = LuceneCommon.GetIndexMetadataPath();
            if (!File.Exists(metadataPath))
            {
                // Create the index and add a timestamp to it that specifies the time at which it was created.
                File.WriteAllBytes(metadataPath, new byte[0]);
            }
            else
            {
                File.SetLastWriteTimeUtc(metadataPath, DateTime.UtcNow);
            }
        }

        protected static void UpdateIndexRefreshTime()
        {
            var metadataPath = LuceneCommon.GetIndexMetadataPath();
            if (File.Exists(metadataPath))
            {
                File.SetCreationTimeUtc(metadataPath, DateTime.UtcNow);
            }
        }


        public Task<int> GetDocumentCount()
        {
            using (IndexReader reader = IndexReader.Open(_directory, readOnly: true))
            {
                return Task.FromResult(reader.NumDocs());
            }
        }


        public Task<long> GetIndexSizeInBytes()
        {
            var path = IndexPath;
            return Task.FromResult(CalculateSize(new DirectoryInfo(path)));
        }


        public void RegisterBackgroundJobs(IList<IJob> jobs, IAppConfiguration configuration)
        {
            if (_getShouldAutoUpdate())
            {
                jobs.Add(
                    new LuceneIndexingJob(
                        frequence: TimeSpan.FromMinutes(10),
                        timeout: TimeSpan.FromMinutes(2),
                        indexingService: this));
            }
        }

        private long CalculateSize(DirectoryInfo dir)
        {
            if (!dir.Exists)
            {
                return 0;
            }

            return
                dir.EnumerateFiles().Sum(f => f.Length) +
                dir.EnumerateDirectories().Select(d => CalculateSize(d)).Sum();
        }
    }
}
