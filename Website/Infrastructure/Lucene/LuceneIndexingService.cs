using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace NuGetGallery
{
    public class LuceneIndexingService : IIndexingService
    {
        private static readonly object indexWriterLock = new object();
        private static readonly TimeSpan indexRecreateInterval = TimeSpan.FromDays(3);
        private static readonly char[] idSeparators = new[] { '.', '-' };
        private static IndexWriter indexWriter;

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
            }

            using (var context = CreateContext())
            {
                var packages = GetPackages(context, lastWriteTime);
                if (packages.Count > 0)
                {
                    AddPackages(packages);
                }
            }
            UpdateLastWriteTime();
        }

        protected internal virtual DbContext CreateContext()
        {
            return new EntitiesContext();
        }

        protected internal virtual List<PackageIndexEntity> GetPackages(DbContext context, DateTime? dateTime)
        {
            if (dateTime == null)
            {
                // If we're creating the index for the first time, fetch the new packages.
                string sql = @"Select p.[Key], pr.Id, p.Title, p.Description, p.Tags, p.FlattenedAuthors as Authors, pr.DownloadCount, p.[Key] as LatestKey
                         from Packages p join PackageRegistrations pr on p.PackageRegistrationKey = pr.[Key]
                         where p.IsLatestStable = 1 or (p.IsLatest = 1 and Not exists (Select 1 from Packages iP where iP.PackageRegistrationKey = p.PackageRegistrationKey and iP.IsLatestStable = 1))";
                return context.Database.SqlQuery<PackageIndexEntity>(sql).ToList();
            }
            else
            {
                string sql = @"Select p.[Key], pr.Id, p.Title, p.Description, p.Tags, p.FlattenedAuthors as Authors, pr.DownloadCount,
                                   LatestKey = CASE When p.IsLatest = 1 then p.[Key] Else (Select pLatest.[Key] from Packages pLatest where pLatest.PackageRegistrationKey = pr.[Key] and pLatest.IsLatest = 1) End
                                   from Packages p join PackageRegistrations pr on p.PackageRegistrationKey = pr.[Key]
                                   where p.LastUpdated > @UpdatedDate";
                return context.Database.SqlQuery<PackageIndexEntity>(sql, new SqlParameter("UpdatedDate", dateTime.Value)).ToList();
            }
        }

        private static void AddPackages(List<PackageIndexEntity> packages)
        {
            foreach (var package in packages)
            {
                if (package.Key != package.LatestKey)
                {
                    indexWriter.DeleteDocuments(new TermQuery(new Term("Key", package.Key.ToString(CultureInfo.InvariantCulture))));
                    continue;
                }

                // If there's an older entry for this package, remove it.
                var document = new Document();

                document.Add(new Field("Key", package.Key.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
                document.Add(new Field("Id-Exact", package.Id.ToLowerInvariant(), Field.Store.NO, Field.Index.NOT_ANALYZED));

                document.Add(new Field("Description", package.Description, Field.Store.NO, Field.Index.ANALYZED));

                var tokenizedId = TokenizeId(package.Id);
                foreach (var idToken in tokenizedId)
                {
                    document.Add(new Field("Id", idToken, Field.Store.NO, Field.Index.ANALYZED));
                }

                // If an element does not have a Title, then add all the tokenized Id components as Title.
                // Lucene's StandardTokenizer does not tokenize items of the format a.b.c which does not play well with things like "xunit.net". 
                // We will feed it values that are already tokenized.
                var titleTokens = String.IsNullOrEmpty(package.Title) ? tokenizedId : package.Title.Split(idSeparators, StringSplitOptions.RemoveEmptyEntries);
                foreach (var idToken in titleTokens)
                {
                    document.Add(new Field("Title", idToken, Field.Store.NO, Field.Index.ANALYZED));
                }

                if (!String.IsNullOrEmpty(package.Tags))
                {
                    document.Add(new Field("Tags", package.Tags, Field.Store.NO, Field.Index.ANALYZED));
                }
                document.Add(new Field("Author", package.Authors, Field.Store.NO, Field.Index.ANALYZED));
                document.Add(new Field("DownloadCount", package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.ANALYZED_NO_NORMS));

                indexWriter.AddDocument(document);
            }
            indexWriter.Commit();
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