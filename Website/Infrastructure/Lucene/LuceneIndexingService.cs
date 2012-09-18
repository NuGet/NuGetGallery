using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Ninject;

namespace NuGetGallery
{
    public class LuceneIndexingService : IIndexingService
    {
        private readonly DbContext _entitiesContext;
        private static readonly object indexWriterLock = new object();
        private static readonly TimeSpan indexRecreateInterval = TimeSpan.FromDays(3);
        private static readonly char[] idSeparators = new[] { '.', '-' };
        private static IndexWriter indexWriter;

        public LuceneIndexingService() : this(new EntitiesContext())
        {
        }

        [Inject]
        public LuceneIndexingService(IEntitiesContext entitiesContext)
        {
            _entitiesContext = (DbContext)entitiesContext;
        }

        public void UpdateIndex()
        {
            DateTime? lastWriteTime = GetLastWriteTime();
            bool creatingIndex = lastWriteTime == null;

            EnsureIndexWriter(creatingIndex);

            if (IndexRequiresRefresh())
            {
                indexWriter.DeleteAll();
                indexWriter.Commit();

                // Reset the lastWriteTime to null. This will allow us to get a fresh copy of all the latest \ latest successful packages
                lastWriteTime = null;

                // Set the index create time to now. This would tell us when we last rebuilt the index.
                UpdateIndexRefreshTime();
            }
            if (_entitiesContext != null)
            {
                var packages = GetPackages(_entitiesContext, lastWriteTime);
                if (packages.Count > 0)
                {
                    AddPackages(packages, creatingIndex: lastWriteTime == null);
                }
            }
            UpdateLastWriteTime();
        }

        protected internal virtual List<PackageIndexEntity> GetPackages(DbContext context, DateTime? lastIndexTime)
        {
            string sql = @"SELECT p.[Key], p.PackageRegistrationKey, pr.Id, p.Title, p.Description, p.Tags, p.FlattenedAuthors as Authors, pr.DownloadCount,
                                  p.IsLatestStable, p.IsLatest, p.Published
                              FROM Packages p JOIN PackageRegistrations pr on p.PackageRegistrationKey = pr.[Key]
                              WHERE ((p.IsLatest = 1) or (p.IsLatestStable = 1)) ";

            object[] parameters;
            if (lastIndexTime == null)
            {
                // First time creation. Pull latest packages without filtering
                parameters = new object[0];
            }
            else
            {
                // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                sql += " AND Exists (Select 1 from dbo.Packages iP where iP.LastUpdated > @UpdatedDate and iP.PackageRegistrationKey = p.PackageRegistrationKey) ";
                parameters = new[] { new SqlParameter("UpdatedDate", lastIndexTime.Value) };
            }
            return context.Database.SqlQuery<PackageIndexEntity>(sql, parameters).ToList();
        }

        private static void AddPackages(List<PackageIndexEntity> packages, bool creatingIndex)
        {
            if (!creatingIndex)
            {
                // If this is not the first time we're creating the index, clear any package registrations for packages we are going to updating.
                var packagesToDelete = from packageRegistrationKey in packages.Select(p => p.PackageRegistrationKey).Distinct()
                                       select new Term("PackageRegistrationKey", packageRegistrationKey.ToString(CultureInfo.InvariantCulture));
                indexWriter.DeleteDocuments(packagesToDelete.ToArray());
            }

            // As per http://stackoverflow.com/a/3894582. The IndexWriter is CPU bound, so we can try and write multiple packages in parallel.
            // The IndexWriter is thread safe and is primarily CPU-bound. 
            Parallel.ForEach(packages, AddPackage);

            indexWriter.Commit();
        }

        private static void AddPackage(PackageIndexEntity package)
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
            var titleTokens = String.IsNullOrEmpty(package.Title) ? tokenizedId : package.Title.Split(idSeparators, StringSplitOptions.RemoveEmptyEntries);
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
            document.Add(new Field("PackageRegistrationKey", package.PackageRegistrationKey.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatest", package.IsLatest.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestStable", package.IsLatestStable.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("PublishedDate", package.Published.Ticks.ToString(), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("DownloadCount", package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            string displayName = String.IsNullOrEmpty(package.Title) ? package.Id : package.Title;
            document.Add(new Field("DisplayName", displayName.ToLower(CultureInfo.CurrentCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));

            indexWriter.AddDocument(document);
        }

        protected static void EnsureIndexWriter(bool creatingIndex)
        {
            if (indexWriter == null)
            {
                lock (indexWriterLock)
                {
                    if (indexWriter == null)
                    {
                        EnsureIndexWriterCore(creatingIndex);
                    }
                }
            }
        }

        private static void EnsureIndexWriterCore(bool creatingIndex)
        {
            if (!Directory.Exists(LuceneCommon.IndexDirectory))
            {
                Directory.CreateDirectory(LuceneCommon.IndexDirectory);
            }

            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            var directoryInfo = new DirectoryInfo(LuceneCommon.IndexDirectory);
            var directory = new Lucene.Net.Store.SimpleFSDirectory(directoryInfo);
            indexWriter = new IndexWriter(directory, analyzer, create: creatingIndex, mfl: IndexWriter.MaxFieldLength.UNLIMITED);
        }

        protected internal bool IndexRequiresRefresh()
        {
            if (File.Exists(LuceneCommon.IndexMetadataPath))
            {
                var creationTime = File.GetCreationTimeUtc(LuceneCommon.IndexMetadataPath);
                return (DateTime.UtcNow - creationTime) > indexRecreateInterval;
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
            var tokens = term.Split(idSeparators, StringSplitOptions.RemoveEmptyEntries);

            // For each token, further attempt to tokenize camelcase values. e.g. .EventStream -> Event, Stream. 
            var result = tokens.Concat(new[] { term })
                               .Concat(tokens.SelectMany(CamelCaseTokenize))
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();
            return result;
        }

        private static IEnumerable<string> CamelCaseTokenize(string term)
        {
            const int MinTokenLength = 3;
            if (term.Length < MinTokenLength)
            {
                yield break;
            }

            int tokenEnd = term.Length;
            for (int i = term.Length - 1; i > 0; i--)
            {
                // If the remainder is fewer than 2 chars or we have a token that is at least 2 chars long, tokenize it.
                if (i < MinTokenLength || (Char.IsUpper(term[i]) && (tokenEnd - i >= MinTokenLength)))
                {
                    if (i < MinTokenLength)
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