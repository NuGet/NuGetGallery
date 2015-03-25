using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using System;
using System.Threading;

namespace NuGet.Indexing
{
    public class SearcherManager
    {
        private readonly object _sync = new object();
        private bool _reopening;
        private Lazy<IndexSearcher> _currentSearcher;

        public Directory Directory { get; private set; }

        public SearcherManager(Directory directory)
        {
            Directory = directory;
            _currentSearcher = new Lazy<IndexSearcher>(() => new IndexSearcher(IndexReader.Open(Directory, true)));
        }

        public void Open()
        {
            Warm(_currentSearcher.Value);
        }

        protected virtual void Warm(IndexSearcher searcher)
        {
        }

        private void StartReopen()
        {
            lock (_sync)
            {
                while (_reopening)
                {
                    Monitor.Wait(_sync);
                }
                _reopening = true;
            }
        }

        private void DoneReopen()
        {
            lock (_sync)
            {
                _reopening = false;
                Monitor.PulseAll(_sync);
            }
        }
        public void MaybeReopen()
        {
            StartReopen();

            try
            {
                IndexSearcher searcher = Get();
                try
                {
                    IndexReader newReader = _currentSearcher.Value.IndexReader.Reopen();
                    if (newReader != _currentSearcher.Value.IndexReader)
                    {
                        IndexSearcher newSearcher = new IndexSearcher(newReader);
                        Warm(newSearcher);
                        SwapSearcher(newSearcher);
                    }
                }
                finally
                {
                    Release(searcher);
                }
            }
            finally
            {
                DoneReopen();
            }
        }

        public IndexSearcher Get()
        {
            lock (_sync)
            {
                _currentSearcher.Value.IndexReader.IncRef();
                return _currentSearcher.Value;
            }
        }

        public void Release(IndexSearcher searcher)
        {
            lock (_sync)
            {
                searcher.IndexReader.DecRef();
            }
        }

        private void SwapSearcher(IndexSearcher newSearcher)
        {
            lock (_sync)
            {
                Release(_currentSearcher.Value);
                _currentSearcher = new Lazy<IndexSearcher>(() => newSearcher);
            }
        }

        public void Close()
        {
            SwapSearcher(null);
        }
    }
}
