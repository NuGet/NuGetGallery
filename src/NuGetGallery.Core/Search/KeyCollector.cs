using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class KeyCollector : Collector
    {
        private int[] _keys;
        private JArray _result;

        public KeyCollector(JArray result)
        {
            _result = result;
        }

        public override bool AcceptsDocsOutOfOrder
        {
            get { return true; }
        }

        public override void Collect(int docID)
        {
            _result.Add(_keys[docID]);
        }

        public override void SetNextReader(Lucene.Net.Index.IndexReader reader, int docBase)
        {
            _keys = FieldCache_Fields.DEFAULT.GetInts(reader, "Key");
        }

        public override void SetScorer(Scorer scorer)
        {
        }
    }
}
