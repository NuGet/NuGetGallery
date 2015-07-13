// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery;
using System.Runtime.Versioning;
using System.Data.SqlClient;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO.Compression;

namespace NuGet.Indexing
{
    public static class PackageIndexing
    {
        private const string _packageTemplate = "{0}/{1}.json";
        const int MaxDocumentsPerCommit = 800;      //  The maximum number of Lucene documents in a single commit. The min size for a segment.
        const int MergeFactor = 10;                 //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        const int MaxMergeDocs = 7999;              //  Except never merge segments that have more docs than this 

        public static TextWriter DefaultTraceWriter = TextWriter.Null;

        public static void CreateFreshIndex(Lucene.Net.Store.Directory directory)
        {
            CreateNewEmptyIndex(directory);
        }

        //  this function will incrementally build an index from the gallery using a high water mark stored in the commit metadata
        //  this function is useful for building a fresh index as in that case it is more efficient than diff-ing approach

        public static void RebuildIndex(string sqlConnectionString, Lucene.Net.Store.Directory directory,TextWriter log = null, PerfEventTracker perfTracker = null)
        {
            perfTracker = perfTracker ?? new PerfEventTracker();
            log = log ?? DefaultTraceWriter;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            using (perfTracker.TrackEvent("RebuildIndex", String.Empty))
            {
                // Empty the index, we're rebuilding
                CreateNewEmptyIndex(directory);

                log.WriteLine("get curated feeds by PackageRegistration");
                IDictionary<int, IEnumerable<string>> feeds = GalleryExport.GetFeedsByPackageRegistration(sqlConnectionString, log, verbose: false);

                int highestPackageKey = 0;
            
                while (true)
                {
                    log.WriteLine("get the checksums from the gallery");
                    IDictionary<int, int> checksums = GalleryExport.FetchGalleryChecksums(sqlConnectionString, highestPackageKey);

                    log.WriteLine("get packages from gallery where the Package.Key > {0}", highestPackageKey);
                    List<Package> packages = GalleryExport.GetPublishedPackagesSince(sqlConnectionString, highestPackageKey, log, verbose: false);

                    if (packages.Count == 0)
                    {
                        break;
                    }

                    log.WriteLine("associate the feeds and checksum data with each packages");
                    List<IndexDocumentData> indexDocumentData = MakeIndexDocumentData(packages, feeds, checksums);
                    highestPackageKey = indexDocumentData.Max(d => d.Package.Key);

                    AddPackagesToIndex(indexDocumentData, directory, log, perfTracker);

                    // Summarize performance
                    // (Save some time by not bothering if the log is "null")
                    if (!ReferenceEquals(TextWriter.Null, log) && !ReferenceEquals(PerfEventTracker.Null, perfTracker))
                    {
                        SummarizePerf(log, perfTracker);
                    }                  
                }
            }

            SummarizePerf(log, perfTracker);

            sw.Stop();
            log.WriteLine("all done, took {0}", sw.Elapsed);
        }

        private static void SummarizePerf(TextWriter log, PerfEventTracker perfTracker)
        {
            log.WriteLine("Perf Summary:");
            foreach (var evt in perfTracker.GetEvents())
            {
                var summary = perfTracker.GetSummary(evt);
                log.WriteLine(" {0} Avg:{5:0.00}ms Max: {1:0.00}ms({2}) Min: {3:0.00}ms({4})",
                    evt,
                    summary.Max.Duration.TotalMilliseconds,
                    summary.Max.Payload,
                    summary.Min.Duration.TotalMilliseconds,
                    summary.Min.Payload,
                    summary.Average.TotalMilliseconds);
            }
            perfTracker.Clear();
        }

        private static void AddPackagesToIndex(List<IndexDocumentData> indexDocumentData, Lucene.Net.Store.Directory directory, TextWriter log, PerfEventTracker perfTracker)
        {
            log.WriteLine("About to add {0} packages", indexDocumentData.Count);

            for (int index = 0; index < indexDocumentData.Count; index += MaxDocumentsPerCommit)
            {
                int count = Math.Min(MaxDocumentsPerCommit, indexDocumentData.Count - index);

                List<IndexDocumentData> rangeToIndex = indexDocumentData.GetRange(index, count);

                AddToIndex(directory, rangeToIndex, log, perfTracker);
            }
        }

        private static void AddToIndex(Lucene.Net.Store.Directory directory, List<IndexDocumentData> rangeToIndex, TextWriter log, PerfEventTracker perfTracker)
        {
            log.WriteLine("begin AddToIndex");

            int highestPackageKey = -1;
            using (IndexWriter indexWriter = CreateIndexWriter(directory, create: false))
            {
                // Just write the document to index. No Facet.
                foreach (IndexDocumentData data in rangeToIndex)
                {
                    indexWriter.AddDocument(CreateLuceneDocument(data));
                }

                highestPackageKey = rangeToIndex.Max(i => i.Package.Key);

                log.WriteLine("about to commit {0} packages", rangeToIndex.Count);

                IDictionary<string, string> commitUserData = indexWriter.GetReader().CommitUserData;

                string lastEditsIndexTime = commitUserData["last-edits-index-time"];

                if (lastEditsIndexTime == null)
                {
                    // this should never happen but if it did Lucene would throw 
                    lastEditsIndexTime = DateTime.MinValue.ToString();
                }

                indexWriter.Commit(CreateCommitMetadata(lastEditsIndexTime, highestPackageKey, rangeToIndex.Count, "add"));

                log.WriteLine("commit done");
            }

            log.WriteLine("end AddToIndex");
        }
      
        public static void CreateNewEmptyIndex(Lucene.Net.Store.Directory directory)
        {
            using (IndexWriter indexWriter = CreateIndexWriter(directory, true))
            {
                indexWriter.Commit(CreateCommitMetadata(DateTime.MinValue, 0, 0, "creation"));
            }
        }

        private static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory, bool create)
        {
            IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.MergeFactor = MergeFactor;
            indexWriter.MaxMergeDocs = MaxMergeDocs;

            indexWriter.SetSimilarity(new CustomSimilarity());           
            return indexWriter;
        }

        private static IDictionary<string, string> CreateCommitMetadata(DateTime lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            return CreateCommitMetadata(lastEditsIndexTime.ToString(), highestPackageKey, count, description);
        }

        private static IDictionary<string, string> CreateCommitMetadata(string lastEditsIndexTime, int highestPackageKey, int count, string description)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();

            commitMetadata.Add("commit-time-stamp", DateTime.UtcNow.ToString("O"));
            commitMetadata.Add("commitTimeStamp", DateTime.UtcNow.ToString("O"));
            commitMetadata.Add("commit-description", description ?? string.Empty);
            commitMetadata.Add("commit-document-count", count.ToString());

            commitMetadata.Add("highest-package-key", highestPackageKey.ToString());
            commitMetadata.Add("last-edits-index-time", lastEditsIndexTime ?? DateTime.MinValue.ToString());

            commitMetadata.Add("MaxDocumentsPerCommit", MaxDocumentsPerCommit.ToString());
            commitMetadata.Add("MergeFactor", MergeFactor.ToString());
            commitMetadata.Add("MaxMergeDocs", MaxMergeDocs.ToString());

            return commitMetadata;
        }

        private static DateTime GetLastEditsIndexTime(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);

            string lastEditsIndexTime;
            if (commitMetadata.TryGetValue("last-edits-index-time", out lastEditsIndexTime))
            {
                return DateTime.Parse(lastEditsIndexTime);
            }

            return DateTime.MinValue;
        }

        private static int GetHighestPackageKey(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);

            string highestPackageKey;
            if (commitMetadata.TryGetValue("highest-package-key", out highestPackageKey))
            {
                return int.Parse(highestPackageKey);
            }

            return 0;
        }

        private static void Add(Document doc, string name, string value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            if (value == null)
            {
                return;
            }

            Field newField = new Field(name, value, store, index, termVector);
            newField.Boost = boost;
            doc.Add(newField);
        }

        private static void Add(Document doc, string name, int value, Field.Store store, Field.Index index, Field.TermVector termVector, float boost = 1.0f)
        {
            Add(doc, name, value.ToString(CultureInfo.InvariantCulture), store, index, termVector, boost);
        }

        private static float DetermineLanguageBoost(string id, string language)
        {
            if (!string.IsNullOrWhiteSpace(language))
            {
                string languageSuffix = "." + language.Trim();
                if (id.EndsWith(languageSuffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    return 0.1f;
                }
            }
            return 1.0f;
        }

        /* Open questions:
        1. Should we propogate curated feed data to V3 ?
        2. Should the Package Key added to Lucene ? Is it used as high water mark or anything ?
        3. How to add "CatalogEntry", "TenantId", "Visibility" values.
        4. Where does the below fields that show up in search query come from :
           "@id": "http://api.nuget.org/v3/registration0/nunit.runners/2.6.4.json",
           "@type": "Package",
           "registration": "http://api.nuget.org/v3/registration0/nunit.runners/index.json",
         */
        private static Document CreateLuceneDocument(IndexDocumentData data)
        {
            Package package = data.Package;

            Document doc = new Document();

            //  Query Fields

            float titleBoost = 3.0f;
            float idBoost = 2.0f;

            if (package.Tags == null)
            {
                titleBoost += 0.5f;
                idBoost += 0.5f;
            }

            string title = package.Title ?? package.PackageRegistration.Id;

            Add(doc, "Id", package.PackageRegistration.Id, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "IdAutocomplete", package.PackageRegistration.Id, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO);
            Add(doc, "IdAutocompletePhrase", "/ " + package.PackageRegistration.Id, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);            
            Add(doc, "TokenizedId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "ShingledId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", package.Version, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", title, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, titleBoost);
            Add(doc, "Tags", package.Tags, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Description", package.Description, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Authors", package.FlattenedAuthors, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Owners", PackageJson.ToJson_Owners(package.PackageRegistration.Owners).ToString(), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);


            Add(doc, "Summary", package.Summary, Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "IconUrl", package.IconUrl, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ProjectUrl", package.ProjectUrl, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "MinClientVersion", package.MinClientVersion, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "ReleaseNotes", package.ReleaseNotes, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Copyright", package.Copyright, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Language", package.Language, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "LicenseUrl", package.LicenseUrl, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "RequiresLicenseAcceptance", package.RequiresLicenseAcceptance.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            Add(doc, "PackageHash", package.Hash, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "PackageHashAlgorithm", package.HashAlgorithm, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "PackageSize", package.PackageFileSize.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);                       

            doc.Add(new NumericField("PublishedDate", Field.Store.YES, true).SetIntValue(int.Parse(package.Published.ToString("yyyyMMdd"))));

            DateTime lastEdited = (DateTime)(package.LastEdited ?? package.Published);
            doc.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEdited.ToString("yyyyMMdd"))));

            string displayName = String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id: package.Title;
            displayName = displayName.ToLower(CultureInfo.CurrentCulture);
            Add(doc, "DisplayName", displayName, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            string packageUrl = string.Format(_packageTemplate, package.PackageRegistration.Id.ToLowerInvariant(), package.Version.ToLowerInvariant());

            Add(doc, "Url", packageUrl.ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Add(doc, "Listed", package.Listed.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "FlattenedDependencies", package.FlattenedDependencies, Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Add(doc, "Dependencies",PackageJson.ToJson_PackageDependencies(package.Dependencies).ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Add(doc, "SupportedFrameworks", PackageJson.ToJson_SupportedFrameworks(package.SupportedFrameworks).ToString(), Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            //  The following fields are added for back compatibility with the V2 gallery
            // Ques: Should any specific format need to be applied for ToString on these DateTime (like how we have in catalog entry).
            Add(doc, "OriginalVersion", package.Version.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalCreated", package.Created.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalLastEdited", package.LastEdited.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "OriginalPublished", package.Published.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            Add(doc, "LicenseNames", package.LicenseNames, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "LicenseReportUrl", package.LicenseReportUrl, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
           
            doc.Boost = DetermineLanguageBoost(package.PackageRegistration.Id, package.Language);

            return doc;
        }
    
        //  helper functions

        public static IDictionary<int, IndexDocumentData> LoadDocumentData(string connectionString, List<int> adds, List<int> updates, List<int> deletes, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums, TextWriter log = null)
        {
            log = log ?? DefaultTraceWriter;

            IDictionary<int, IndexDocumentData> packages = new Dictionary<int, IndexDocumentData>();

            List<Package> addsPackages = GalleryExport.GetPackages(connectionString, adds, log, verbose: false);
            List<IndexDocumentData> addsIndexDocumentData = MakeIndexDocumentData(addsPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in addsIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            List<Package> updatesPackages = GalleryExport.GetPackages(connectionString, updates, log, verbose: false);
            List<IndexDocumentData> updatesIndexDocumentData = MakeIndexDocumentData(updatesPackages, feeds, checksums);
            foreach (IndexDocumentData indexDocumentData in updatesIndexDocumentData)
            {
                packages.Add(indexDocumentData.Package.Key, indexDocumentData);
            }

            return packages;
        }

        public static List<IndexDocumentData> MakeIndexDocumentData(IList<Package> packages, IDictionary<int, IEnumerable<string>> feeds, IDictionary<int, int> checksums)
        {
            Func<int, IEnumerable<string>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<string> ret = null;
                feeds.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            List<IndexDocumentData> result = packages
                .Select(p => new IndexDocumentData { Package = p, Checksum = checksums[p.Key], Feeds = GetFeeds(p.PackageRegistrationKey) })
                .ToList();

            return result;
        }
    }
}
