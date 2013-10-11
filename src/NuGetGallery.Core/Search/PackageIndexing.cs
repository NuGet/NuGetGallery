using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class PackageIndexing
    {
        public static void CreateNewEmptyIndex(Lucene.Net.Store.Directory directory)
        {
            using (IndexWriter indexWriter = new IndexWriter(directory, new PackageAnalyzer(), true, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                indexWriter.Commit(PackageIndexing.CreateCommitMetadata(0, 0));
            }
        }

        public static IDictionary<string, string> CreateCommitMetadata(int count, int maxKey)
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();
            commitMetadata.Add("index-creation-time", DateTime.UtcNow.ToString());
            commitMetadata.Add("document-count", count.ToString());
            commitMetadata.Add("max-package-key", maxKey.ToString());
            return commitMetadata;
        }

        public static int GetLastMaxKey(Lucene.Net.Store.Directory directory)
        {
            IDictionary<string, string> commitMetadata = IndexReader.GetCommitUserData(directory);
            if (commitMetadata != null)
            {
                string s;
                if (commitMetadata.TryGetValue("max-package-key", out s))
                {
                    int maxKey = int.Parse(s);
                    return maxKey;
                }
            }
            return 0;
        }

        public static Document CreateLuceneDocument(Package package, IEnumerable<string> feeds, int rank, IDictionary<string, int> projectTypeRankings)
        {
            Document doc = new Document();

            //  Query Fields

            float idBoost = (package.Title != null) ? 1.5f : 3.0f;

            Add(doc, "Id", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "TokenizedId", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Version", package.Version, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, idBoost);
            Add(doc, "Title", package.Title, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Tags", package.Tags, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS, 1.5f);
            Add(doc, "Description", package.Description, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Add(doc, "Authors", package.FlattenedAuthors, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            foreach (User owner in package.PackageRegistration.Owners)
            {
                Add(doc, "Owners", owner.Username, Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            }

            //  Sorting:

            doc.Add(new NumericField("PublishedDate", Field.Store.YES, true).SetIntValue(int.Parse(package.Published.ToString("yyyyMMdd"))));

            DateTime lastEdited = package.LastEdited ?? package.Published;
            doc.Add(new NumericField("EditedDate", Field.Store.YES, true).SetIntValue(int.Parse(lastEdited.ToString("yyyyMMdd"))));

            string displayName = String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            displayName = displayName.ToLower(CultureInfo.CurrentCulture);
            Add(doc, "DisplayName", displayName, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            //  Facets:

            Add(doc, "IsLatest", package.IsLatest ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            Add(doc, "IsLatestStable", package.IsLatestStable ? 1 : 0, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);

            if (feeds != null)
            {
                foreach (string feed in feeds)
                {
                    Add(doc, "CuratedFeed", feed, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
                }
            }

            foreach (PackageFramework packageFramework in package.SupportedFrameworks)
            {
                Add(doc, "SupportedFramework", packageFramework.TargetFramework, Field.Store.NO, Field.Index.NOT_ANALYZED, Field.TermVector.NO);
            }

            //  Add ranking fields

            doc.Add(new NumericField("Rank", Field.Store.YES, true).SetIntValue(rank));

            foreach (KeyValuePair<string, int> projectTypeRanking in projectTypeRankings)
            {
                doc.Add(new NumericField(projectTypeRanking.Key, Field.Store.YES, true).SetIntValue(projectTypeRanking.Value));
            }

            //  Data we wnat to store in index - these cannot be queried

            JObject obj = PackageJson.ToJson(package);

            //  add these to help with debugging boosting

            obj.Add("Rank", rank);
            foreach (KeyValuePair<string, int> item in projectTypeRankings)
            {
                obj.Add(item.Key, item.Value);
            }

            string data = obj.ToString();

            Add(doc, "Data", data, Field.Store.YES, Field.Index.NO, Field.TermVector.NO);

            return doc;
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
    }
}
