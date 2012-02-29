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
        private static readonly TimeSpan indexRecreateTime = TimeSpan.FromDays(3);
        private static readonly char[] idSeparators = new[] { '.', '-' };

        public void UpdateIndex()
        {
            DateTime? createdTime = GetIndexCreationTime();
            if (createdTime.HasValue && (DateTime.UtcNow - createdTime > indexRecreateTime))
            {
                ClearLuceneDirectory();
            }

            DateTime? lastWriteTime = GetLastWriteTime();
            bool creatingIndex = lastWriteTime == null;
            using (var context = CreateContext())
            {
                var packages = GetPackages(context, lastWriteTime);
                if (packages.Any())
                {
                    EnsureIndexDirectory();
                    WriteIndex(creatingIndex, packages);
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

        protected internal virtual void WriteIndex(bool creatingIndex, List<PackageIndexEntity> packages)
        {
            using (var directory = new LuceneFileSystem(LuceneCommon.IndexDirectory))
            {
                var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
                var indexWriter = new IndexWriter(directory, analyzer, create: creatingIndex, mfl: IndexWriter.MaxFieldLength.UNLIMITED);
                AddPackages(indexWriter, packages);
                indexWriter.Close();
            }
        }

        private static void AddPackages(IndexWriter indexWriter, List<PackageIndexEntity> packages)
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
                document.Add(new Field("Id-Exact", package.Id, Field.Store.NO, Field.Index.ANALYZED));

                document.Add(new Field("Description", package.Description, Field.Store.NO, Field.Index.ANALYZED));

                foreach (var idToken in TokenizeId(package.Id))
                {
                    document.Add(new Field("Id", idToken, Field.Store.NO, Field.Index.ANALYZED));
                }

                if (!String.IsNullOrEmpty(package.Title))
                {
                    document.Add(new Field("Title", package.Title, Field.Store.NO, Field.Index.ANALYZED));
                }
                if (!String.IsNullOrEmpty(package.Tags))
                {
                    document.Add(new Field("Tags", package.Tags, Field.Store.NO, Field.Index.ANALYZED));
                }
                document.Add(new Field("Author", package.Authors, Field.Store.NO, Field.Index.ANALYZED));
                document.Add(new Field("DownloadCount", package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.ANALYZED_NO_NORMS));

                indexWriter.AddDocument(document);
            }
        }

        protected internal virtual void EnsureIndexDirectory()
        {
            if (!Directory.Exists(LuceneCommon.IndexDirectory))
            {
                Directory.CreateDirectory(LuceneCommon.IndexDirectory);
            }
        }

        protected internal virtual DateTime? GetIndexCreationTime()
        {
            if (File.Exists(LuceneCommon.IndexMetadataPath))
            {
                var text = File.ReadLines(LuceneCommon.IndexMetadataPath).FirstOrDefault();
                DateTime dateTime;
                if (!String.IsNullOrEmpty(text) && DateTime.TryParseExact(text, "R", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                {
                    return dateTime;
                }
            }
            
            return null;
        }

        protected internal virtual void ClearLuceneDirectory()
        {
            if (Directory.Exists(LuceneCommon.IndexDirectory))
            {
                Directory.Delete(LuceneCommon.IndexDirectory, recursive: true);
            }
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
                if (Directory.Exists(LuceneCommon.IndexMetadataPath))
                {
                    // If the directoey exists, then assume that the index has been created.
                    File.WriteAllText(LuceneCommon.IndexMetadataPath, DateTime.UtcNow.ToString("R"));
                }
            }
            else
            {
                File.SetLastWriteTimeUtc(LuceneCommon.IndexMetadataPath, DateTime.UtcNow);
            }
        }

        internal static IEnumerable<string> TokenizeId(string term)
        {
            var result = CamelCaseTokenize(term).SelectMany(s => s.Split(idSeparators, StringSplitOptions.RemoveEmptyEntries)).ToList();
            if (result.Count == 1)
            {
                return Enumerable.Empty<string>();
            }
            return result;
        }

        private static IEnumerable<string> CamelCaseTokenize(string term)
        {
            if (term.Length < 2)
            {
                yield break;
            }

            int tokenStart = 0;
            for (int i = 1; i < term.Length; i++)
            {
                if (Char.IsUpper(term[i]) && (i - tokenStart > 2))
                {
                    yield return term.Substring(tokenStart, i - tokenStart);
                    tokenStart = i;
                }
            }
            if (term.Length - tokenStart < 2)
            {
                yield break;
            }
            yield return term.Substring(tokenStart);
        }
    }
}