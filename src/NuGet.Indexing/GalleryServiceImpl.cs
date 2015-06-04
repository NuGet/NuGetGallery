// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public class GalleryServiceImpl
    {
        public static JToken Query(IOwinContext context, NuGetSearcherManager searcherManager)
        {
            Trace.TraceInformation("Search: {0}", context.Request.QueryString);

            string q = context.Request.Query["q"] ?? string.Empty;

            string projectType = context.Request.Query["projectType"] ?? string.Empty;

            string sortBy = context.Request.Query["sortBy"] ?? string.Empty;

            bool includePrerelease;
            if (!bool.TryParse(context.Request.Query["prerelease"], out includePrerelease))
            {
                includePrerelease = false;
            }

            bool countOnly;
            if (!bool.TryParse(context.Request.Query["countOnly"], out countOnly))
            {
                countOnly = false;
            }

            string feed = context.Request.Query["feed"] ?? "none";

            int skip;
            if (!int.TryParse(context.Request.Query["skip"], out skip))
            {
                skip = 0;
            }

            int take;
            if (!int.TryParse(context.Request.Query["take"], out take))
            {
                take = 20;
            }

            bool ignoreFilter;
            if (!bool.TryParse(context.Request.Query["ignoreFilter"], out ignoreFilter))
            {
                ignoreFilter = false;
            }

            //string fxName = context.Request.Query["supportedFramework"];
            //FrameworkName supportedFramework = null;
            //if (!String.IsNullOrEmpty(fxName))
            //{
            //    supportedFramework = VersionUtility.ParseFrameworkName(fxName);
            //}
            //if (supportedFramework == null || !SearcherManager.GetFrameworks().Contains(supportedFramework))
            //{
            //    supportedFramework = FrameworksList.AnyFramework;
            //}

            string supportedFramework = string.Empty;

            return QuerySearch(searcherManager, q, countOnly, projectType, supportedFramework, includePrerelease, sortBy, skip, take, ignoreFilter);
        }

        public static JToken QuerySearch(NuGetSearcherManager searcherManager, string q, bool countOnly, string projectType, string supportedFramework, bool includePrerelease, string sortBy, int skip, int take, bool ignoreFilter)
        {
            var searcher = searcherManager.Get();
            try
            {
                Filter filter = ignoreFilter ? null : searcherManager.GetFilter(searcher, includePrerelease, null);

                Query query = LuceneQueryCreator.Parse(q, false);

                if (countOnly)
                {
                    return DocumentCountImpl(searcher, query, filter);
                }
                else
                {
                    IDictionary<string, int> rankings = searcherManager.GetRankings(projectType);

                    return ListDocumentsImpl(searcher, query, rankings, filter, sortBy, skip, take, searcherManager);
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static Query MakeQuery(string q, NuGetSearcherManager searcherManager)
        {
            Query query = LuceneQueryCreator.Parse(q, false);
            Query boostedQuery = new RankingScoreQuery(query, searcherManager.GetRankings());
            return boostedQuery;
        }

        private static JToken DocumentCountImpl(IndexSearcher searcher, Query query, Filter filter)
        {
            TopDocs topDocs = searcher.Search(query, filter, 1);
            return MakeCountResult(topDocs.TotalHits);
        }

        private static JToken ListDocumentsImpl(NuGetIndexSearcher searcher, Query query, IDictionary<string, int> rankings, Filter filter, string sortBy, int skip, int take, NuGetSearcherManager manager)
        {
            Query boostedQuery = new RankingScoreQuery(query, rankings);

            int nDocs = skip + take;
            Sort sort = GetSort(sortBy);

            TopDocs topDocs = (sort == null) ?
                searcher.Search(boostedQuery, filter, nDocs) :
                searcher.Search(boostedQuery, filter, nDocs, sort);

            return MakeResults(searcher, topDocs, skip, take, boostedQuery, rankings, manager);
        }

        private static readonly Dictionary<string, Func<Sort>> _sorts = new Dictionary<string, Func<Sort>>(StringComparer.OrdinalIgnoreCase) {
            {"lastEdited", () => new Sort(new SortField("EditedDate", SortField.INT, reverse: true))},
            {"published", () => new Sort(new SortField("PublishedDate", SortField.INT, reverse: true))},
            {"title-asc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: false))},
            {"title-desc", () => new Sort(new SortField("DisplayName", SortField.STRING, reverse: true))},
        };

        private static Sort GetSort(string sortBy)
        {
            if (String.IsNullOrEmpty(sortBy))
            {
                return null;
            }
            Func<Sort> sort;
            if (!_sorts.TryGetValue(sortBy, out sort))
            {
                return null;
            }
            return sort();
        }

        private static JToken MakeCountResult(int totalHits)
        {
            return new JObject { { "totalHits", totalHits } };
        }

        private static JToken MakeResults(NuGetIndexSearcher searcher, TopDocs topDocs, int skip, int take, Query boostedQuery, IDictionary<string, int> rankings, NuGetSearcherManager manager)
        {
            JArray array = new JArray();

            Tuple<OpenBitSet, OpenBitSet> latestBitSets = manager.GetBitSets(searcher, null);

            for (int i = skip; i < Math.Min(skip + take, topDocs.ScoreDocs.Length); i++)
            {
                ScoreDoc scoreDoc = topDocs.ScoreDocs[i];

                Document document = searcher.Doc(scoreDoc.Doc);

                Tuple<int, int> downloadCounts = manager.GetDownloadCounts(document.Get("Id"), document.Get("Version"));

                JObject packageObj = new JObject();

                JObject registrationObj = new JObject();

                registrationObj.Add("Id", document.Get("Id"));
                registrationObj.Add("DownloadCount", downloadCounts.Item1);

                packageObj.Add("PackageRegistration", registrationObj);

                packageObj.Add("Version", document.Get("OriginalVersion"));
                packageObj.Add("NormalizedVersion", document.Get("Version"));
                packageObj.Add("Title", document.Get("Title"));
                packageObj.Add("Description", document.Get("Description"));
                packageObj.Add("Summary", document.Get("Summary"));
                packageObj.Add("Authors", document.Get("Authors"));
                packageObj.Add("Copyright", document.Get("Copyright"));
                packageObj.Add("Language", document.Get("Language"));
                packageObj.Add("Tags", document.Get("Tags"));
                packageObj.Add("ReleaseNotes", document.Get("ReleaseNotes"));
                packageObj.Add("ProjectUrl", document.Get("ProjectUrl"));
                packageObj.Add("IconUrl", document.Get("IconUrl"));
                packageObj.Add("IsLatestStable", latestBitSets.Item1.Get(scoreDoc.Doc));
                packageObj.Add("IsLatest", latestBitSets.Item2.Get(scoreDoc.Doc));
                packageObj.Add("Listed", bool.Parse(document.Get("Listed") ?? "true"));
                packageObj.Add("Created", document.Get("OriginalCreated"));
                packageObj.Add("Published", document.Get("OriginalPublished"));
                packageObj.Add("LastUpdated", document.Get("OriginalPublished"));
                packageObj.Add("LastEdited", document.Get("OriginalEditedDate"));
                packageObj.Add("DownloadCount", downloadCounts.Item2);
                packageObj.Add("FlattenedDependencies", "");                                         //TODO: data is missing from index
                packageObj.Add("Dependencies", new JArray());                                        //TODO: data is missing from index
                packageObj.Add("SupportedFrameworks", new JArray());                                 //TODO: data is missing from index
                packageObj.Add("MinClientVersion", document.Get("MinClientVersion"));
                packageObj.Add("Hash", document.Get("PackageHash"));
                packageObj.Add("HashAlgorithm", document.Get("PackageHashAlgorithm"));
                packageObj.Add("PackageFileSize", int.Parse(document.Get("PackageSize") ?? "0"));
                packageObj.Add("LicenseUrl", document.Get("LicenseUrl"));
                packageObj.Add("RequiresLicenseAcceptance", bool.Parse(document.Get("RequiresLicenseAcceptance") ?? "true"));
                packageObj.Add("LicenseNames", document.Get("LicenseNames"));
                packageObj.Add("LicenseReportUrl", document.Get("LicenseReportUrl"));
                packageObj.Add("HideLicenseReport", bool.Parse(document.Get("HideLicenseReport") ?? "true"));   //TODO: data is missing from index

                array.Add(packageObj);
            }

            return array;
        }
    }
}
