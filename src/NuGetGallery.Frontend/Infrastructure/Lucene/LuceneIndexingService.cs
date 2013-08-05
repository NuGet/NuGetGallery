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
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class LuceneIndexingService : IIndexingService
    {
        private static readonly object IndexWriterLock = new object();

        private static readonly TimeSpan IndexRecreateInterval = TimeSpan.FromHours(3);

        private static ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter> WriterCache =
            new ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter>();

        private Lucene.Net.Store.Directory _directory;
        private IndexWriter _indexWriter;
        private IEntityRepository<Package> _packageRepository;
        private IEntityRepository<CuratedPackage> _curatedPackageRepository;

        private IDiagnosticsSource Trace { get; set; }

        public string IndexPath
        {
            get { return LuceneCommon.GetDirectoryLocation(); }
        }

        public LuceneIndexingService(
            IEntityRepository<Package> packageSource,
            IEntityRepository<CuratedPackage> curatedPackageSource,
            Lucene.Net.Store.Directory directory,
			IDiagnosticsService diagnostics)
        {
            _packageRepository = packageSource;
            _curatedPackageRepository = curatedPackageSource;
            _directory = directory;
            Trace = diagnostics.SafeGetSource("LuceneIndexingService");
        }

        public void UpdateIndex()
        {
            UpdateIndex(forceRefresh: false);
        }

        public void UpdateIndex(bool forceRefresh)
        {
            DateTime? lastWriteTime = GetLastWriteTime();

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
                AddPackages(packages, creatingIndex: lastWriteTime == null);
            }

            UpdateLastWriteTime();
        }

        public void UpdatePackage(Package package)
        {
            string id = package.PackageRegistration.Id;
            string version = package.Version;
            int key = package.Key;

            var packageRegistrationKey = package.PackageRegistrationKey;
            var updateTerm = new Term("PackageRegistrationKey", packageRegistrationKey.ToString(CultureInfo.InvariantCulture));

            if (!package.IsLatest || !package.IsLatestStable)
            {
                // Someone passed us in a version which was e.g. just unlisted? Or just not the latest version which is what we want to index. Doesn't really matter. We'll find one to index.
                package = _packageRepository.GetAll()
                    .Where(p => (p.IsLatest || p.IsLatestStable) && p.PackageRegistrationKey == packageRegistrationKey)
                    .Include(p => p.PackageRegistration)
                    .Include(p => p.PackageRegistration.Owners)
                    .Include(p => p.SupportedFrameworks)
                    .FirstOrDefault();
            }

            // Just update the provided package
            using (Trace.Activity(String.Format(CultureInfo.CurrentCulture, "Updating Lucene Index for: {0} {1} [PackageKey:{2}]", id, version, key)))
            {
                EnsureIndexWriter(creatingIndex: false);
                if (package != null)
                {
                    var indexEntity = new PackageIndexEntity(package);
                    Trace.Information(String.Format(CultureInfo.CurrentCulture, "Updating Document: {0}", updateTerm.ToString()));
                    _indexWriter.UpdateDocument(updateTerm, indexEntity.ToDocument());
                }
                else
                {
                    Trace.Information(String.Format(CultureInfo.CurrentCulture, "Deleting Document: {0}", updateTerm.ToString()));
                    _indexWriter.DeleteDocuments(updateTerm);
                }
                _indexWriter.Commit();
            }
        }

        private List<PackageIndexEntity> GetPackages(DateTime? lastIndexTime)
        {
            // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
            // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
            // update independent of the package.

            IQueryable<Package> set = _packageRepository.GetAll()
                .Where(p => p.IsLatest || p.IsLatestStable)  // which implies that p.IsListed by the way!
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            if (lastIndexTime.HasValue)
            {
                // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                set = set.Where(
                    p => p.PackageRegistration.Packages.Any(
                        p2 => p2.LastUpdated > lastIndexTime));
            }

            var list = set.ToList();

            var curatedFeedsPerPackageRegistration = _curatedPackageRepository.GetAll()
                .Select(cp => new { cp.PackageRegistrationKey, cp.CuratedFeedKey })
                .GroupBy(x => x.PackageRegistrationKey)
                .ToDictionary(group => group.Key, element => element.Select(x => x.CuratedFeedKey));

            Func<int, IEnumerable<int>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<int> ret = null;
                curatedFeedsPerPackageRegistration.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            var packagesForIndexing = list.Select(
                p => new PackageIndexEntity
                {
                    Package = p,
                    CuratedFeedKeys = GetFeeds(p.PackageRegistrationKey)
                });

            return packagesForIndexing.ToList();
        }

        public void AddPackages(IList<PackageIndexEntity> packages, bool creatingIndex)
        {
            if (!creatingIndex)
            {
                // If this is not the first time we're creating the index, clear any package registrations for packages we are going to updating.
                var packagesToDelete = from packageRegistrationKey in packages.Select(p => p.Package.PackageRegistrationKey).Distinct()
                                       select new Term("PackageRegistrationKey", packageRegistrationKey.ToString(CultureInfo.InvariantCulture));
                _indexWriter.DeleteDocuments(packagesToDelete.ToArray());
            }

            // As per http://stackoverflow.com/a/3894582. The IndexWriter is CPU bound, so we can try and write multiple packages in parallel.
            // The IndexWriter is thread safe and is primarily CPU-bound.
            Parallel.ForEach(packages, AddPackage);

            _indexWriter.Commit();
        }

        public virtual DateTime? GetLastWriteTime()
        {
            var metadataPath = LuceneCommon.GetIndexMetadataPath();
            if (!File.Exists(metadataPath))
            {
                return null;
            }
            return File.GetLastWriteTimeUtc(metadataPath);
        }

        private void AddPackage(PackageIndexEntity packageInfo)
        {
            _indexWriter.AddDocument(packageInfo.ToDocument());
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


        public int GetDocumentCount()
        {
            using (IndexReader reader = IndexReader.Open(_directory, readOnly: true))
            {
                return reader.NumDocs();
            }
        }


        public long GetIndexSizeInBytes()
        {
            var path = IndexPath;
            return CalculateSize(new DirectoryInfo(path));
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
