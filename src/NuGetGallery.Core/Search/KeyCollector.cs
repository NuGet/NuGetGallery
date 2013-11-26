using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class KeyCollector : Collector
    {
        private int[] _keys;
        private int[] _checksums;
        private JObject _result;

        public KeyCollector(JObject result)
        {
            _result = result;
        }

        public override bool AcceptsDocsOutOfOrder
        {
            get { return true; }
        }

        public override void Collect(int docID)
        {
            _result.Add(_keys[docID].ToString(), _checksums[docID]);
        }

        public override void SetNextReader(Lucene.Net.Index.IndexReader reader, int docBase)
        {
            _keys = FieldCache_Fields.DEFAULT.GetInts(reader, "Key");
            _checksums = FieldCache_Fields.DEFAULT.GetInts(reader, "Checksum");
        }

        public override void SetScorer(Scorer scorer)
        {
        }
    }
}
