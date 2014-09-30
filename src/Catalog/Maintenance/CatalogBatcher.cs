using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class CatalogBatcher
    {
        int _batchSize;
        List<CatalogItem> _currentBatch;
        AppendOnlyCatalogWriter _writer;

        public CatalogBatcher(int batchSize, AppendOnlyCatalogWriter writer)
        {
            _batchSize = batchSize;
            _writer = writer;
            _currentBatch = new List<CatalogItem>();
            Total = 0;
        }

        public async Task Add(CatalogItem item)
        {
            _currentBatch.Add(item);

            if (_currentBatch.Count == _batchSize)
            {
                await SubmitCurrentBatch();
                _currentBatch.Clear();
            }
        }

        public async Task Complete()
        {
            if (_currentBatch.Count > 0)
            {
                await SubmitCurrentBatch();
                _currentBatch.Clear();
            }
        }

        public int Total
        {
            get;
            private set;
        }

        protected virtual Task SubmitCurrentBatch()
        {
            Total += _currentBatch.Count;

            foreach (var item in _currentBatch)
            {
                _writer.Add(item);
            }

            return _writer.Commit();
        }
    }
}
