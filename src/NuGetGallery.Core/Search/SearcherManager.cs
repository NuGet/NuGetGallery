using Lucene.Net.Index;
using Lucene.Net.Search;
using System;
using System.Threading;

namespace NuGetGallery
{
    public class SearcherManager
    {
        private object _sync = new object();

        private IndexSearcher _currentSearcher;

        public SearcherManager(Lucene.Net.Store.Directory directory)
        {
            _currentSearcher = new IndexSearcher(IndexReader.Open(directory, true));

            Warm(_currentSearcher);
        }

        protected virtual void Warm(IndexSearcher searcher)
        {
        }

        private bool _reopening = false;

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
                    IndexReader newReader = _currentSearcher.IndexReader.Reopen();
                    if (newReader != _currentSearcher.IndexReader)
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
                _currentSearcher.IndexReader.IncRef();
                return _currentSearcher;
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
                Release(_currentSearcher);
                _currentSearcher = newSearcher;
            }
        }

        public void Close()
        {
            SwapSearcher(null);
        }
    }
}
