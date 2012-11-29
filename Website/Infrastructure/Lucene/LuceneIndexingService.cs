using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class LuceneIndexingService : IIndexingService
    {
        private static readonly object IndexWriterLock = new object();
        private static readonly TimeSpan IndexRecreateInterval = TimeSpan.FromDays(3);
        private static readonly char[] IdSeparators = new[] { '.', '-' };

        private Lucene.Net.Store.Directory _directory;
        private IndexWriter _indexWriter;
        private IPackageSource _packageSource;

        static readonly ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter> WriterCache = 
            new ConcurrentDictionary<Lucene.Net.Store.Directory, IndexWriter>();

        public LuceneIndexingService(
            IPackageSource packageSource,
            Lucene.Net.Store.Directory directory)
        {
            _packageSource = packageSource;
            _directory = directory;
        }

        public void UpdateIndex()
        {
            UpdateIndex(forceRefresh: false);
        }

        internal void UpdateIndex(bool forceRefresh)
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

        private List<PackageIndexEntity> GetPackages(DateTime? lastIndexTime)
        {
            // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
            // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
            // update independent of the package.

            IQueryable<Package> packagesForIndexing = _packageSource.GetPackagesForIndexing(lastIndexTime);

            var query = packagesForIndexing.Select(p =>
                new PackageIndexEntity
                {
                    Key = p.Key,
                    PackageRegistrationKey = p.PackageRegistrationKey,
                    Id = p.PackageRegistration.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Tags = p.Tags,
                    Authors = p.FlattenedAuthors,
                    DownloadCount = p.DownloadCount,
                    IsLatest = p.IsLatest,
                    IsLatestStable = p.IsLatestStable,
                    Published = p.Published,
                });

            return query.ToList();
        }

        private void AddPackages(List<PackageIndexEntity> packages, bool creatingIndex)
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

        private void AddPackage(PackageIndexEntity package)
        {
            var document = new Document();

            var field = new Field("Id-Exact", package.Id.ToLowerInvariant(), Field.Store.NO, Field.Index.NOT_ANALYZED);
            field.SetBoost(2.5f);
            document.Add(field);

            field = new Field("Description", package.Description, Field.Store.NO, Field.Index.ANALYZED);
            field.SetBoost(0.1f);
            document.Add(field);

            var tokenizedId = TokenizeId(package.Id);
            foreach (var idToken in tokenizedId)
            {
                field = new Field("Id", idToken, Field.Store.NO, Field.Index.ANALYZED);
                document.Add(field);
            }

            // If an element does not have a Title, then add all the tokenized Id components as Title.
            // Lucene's StandardTokenizer does not tokenize items of the format a.b.c which does not play well with things like "xunit.net". 
            // We will feed it values that are already tokenized.
            var titleTokens = String.IsNullOrEmpty(package.Title)
                                  ? tokenizedId
                                  : package.Title.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var idToken in titleTokens)
            {
                field = new Field("Title", idToken, Field.Store.NO, Field.Index.ANALYZED);
                field.SetBoost(0.9f);
                document.Add(field);
            }

            if (!String.IsNullOrEmpty(package.Tags))
            {
                field = new Field("Tags", package.Tags, Field.Store.NO, Field.Index.ANALYZED);
                field.SetBoost(0.8f);
                document.Add(field);
            }
            document.Add(new Field("Author", package.Authors, Field.Store.NO, Field.Index.ANALYZED));

            // Fields meant for filtering and sorting
            document.Add(new Field("Key", package.Key.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(
                new Field(
                    "PackageRegistrationKey",
                    package.PackageRegistrationKey.ToString(CultureInfo.InvariantCulture),
                    Field.Store.NO,
                    Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatest", package.IsLatest.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestStable", package.IsLatestStable.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("PublishedDate", package.Published.Ticks.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(
                new Field("DownloadCount", package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            string displayName = String.IsNullOrEmpty(package.Title) ? package.Id : package.Title;
            document.Add(new Field("DisplayName", displayName.ToLower(CultureInfo.CurrentCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));

            _indexWriter.AddDocument(document);
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
            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            _indexWriter = new IndexWriter(_directory, analyzer, create: creatingIndex, mfl: IndexWriter.MaxFieldLength.UNLIMITED);

            // Should always be add, due to locking
            var got = WriterCache.GetOrAdd(_directory, _indexWriter);
            Debug.Assert(got == _indexWriter);
        }

        protected internal bool IndexRequiresRefresh()
        {
            if (File.Exists(LuceneCommon.IndexMetadataPath))
            {
                var creationTime = File.GetCreationTimeUtc(LuceneCommon.IndexMetadataPath);
                return (DateTime.UtcNow - creationTime) > IndexRecreateInterval;
            }

            // If we've never created the index, it needs to be refreshed.
            return true;
        }

        protected internal virtual DateTime? GetLastWriteTime()
        {
            if (!File.Exists(LuceneCommon.IndexMetadataPath))
            {
                return null;
            }
            return File.GetLastWriteTimeUtc(LuceneCommon.IndexMetadataPath);
        }

        protected internal virtual void UpdateLastWriteTime()
        {
            if (!File.Exists(LuceneCommon.IndexMetadataPath))
            {
                // Create the index and add a timestamp to it that specifies the time at which it was created.
                File.WriteAllBytes(LuceneCommon.IndexMetadataPath, new byte[0]);
            }
            else
            {
                File.SetLastWriteTimeUtc(LuceneCommon.IndexMetadataPath, DateTime.UtcNow);
            }
        }

        protected void UpdateIndexRefreshTime()
        {
            if (File.Exists(LuceneCommon.IndexMetadataPath))
            {
                File.SetCreationTimeUtc(LuceneCommon.IndexMetadataPath, DateTime.UtcNow);
            }
        }

        internal static IEnumerable<string> TokenizeId(string term)
        {
            // First tokenize the result by id-separators. For e.g. tokenize SignalR.EventStream as SignalR and EventStream
            var tokens = term.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries);

            // For each token, further attempt to tokenize camelcase values. e.g. .EventStream -> Event, Stream. 
            var result = tokens.Concat(new[] { term })
                .Concat(tokens.SelectMany(CamelCaseTokenize))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return result;
        }

        private static IEnumerable<string> CamelCaseTokenize(string term)
        {
            const int minTokenLength = 3;
            if (term.Length < minTokenLength)
            {
                yield break;
            }

            int tokenEnd = term.Length;
            for (int i = term.Length - 1; i > 0; i--)
            {
                // If the remainder is fewer than 2 chars or we have a token that is at least 2 chars long, tokenize it.
                if (i < minTokenLength || (Char.IsUpper(term[i]) && (tokenEnd - i >= minTokenLength)))
                {
                    if (i < minTokenLength)
                    {
                        // If the remainder is smaller than 2 chars, just return the entire string
                        i = 0;
                    }

                    yield return term.Substring(i, tokenEnd - i);
                    tokenEnd = i;
                }
            }

            // Finally return the term in entirety
            yield return term;
        }
    }
}