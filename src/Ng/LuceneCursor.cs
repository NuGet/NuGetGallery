using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ng
{
    public class LuceneCursor : ReadWriteCursor
    {
        Lucene.Net.Store.Directory _directory;
        DateTime _defaultValue;

        public LuceneCursor(Lucene.Net.Store.Directory directory, DateTime defaultValue)
        {
            _directory = directory;
            _defaultValue = defaultValue;
        }

        public override Task Save()
        {
            //  no-op because we will do the Save in the Lucene.Commit

            return Task.FromResult(true);
        }

        public override Task Load()
        {
            if (IndexReader.IndexExists(_directory))
            {
                IDictionary<string, string> commitUserData;
                using (IndexWriter indexWriter = new IndexWriter(_directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), false, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    commitUserData = indexWriter.GetReader().CommitUserData;
                }

                string value;
                if (commitUserData.TryGetValue("commitTimeStamp", out value))
                {
                    Value = DateTime.Parse(value);
                }
                else
                {
                    Value = _defaultValue;
                }
            }
            else
            {
                Value = _defaultValue;
            }
            return Task.FromResult(true);
        }
    }
}
