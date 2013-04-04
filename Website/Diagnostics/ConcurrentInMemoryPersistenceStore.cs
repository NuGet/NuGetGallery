using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using Glimpse.Core.Framework;

namespace NuGetGallery.Diagnostics
{
    // Could make this even faster by using a Ring Buffer of a fixed size...
    public class ConcurrentInMemoryPersistenceStore : IPersistenceStore
    {
        public static readonly int DefaultBufferSize = 100;

        private GlimpseMetadata _metadata;
        private ConcurrentQueue<GlimpseRequest> _requests = new ConcurrentQueue<GlimpseRequest>();
        private int _bufferSize;

        // Lock to be taken ONLY WHEN NECESSARY. Why use a lock when we're also using ConcurrentQueue?
        // We only need to lock to prevent multiple threads En/Dequeueing objects at the same time. While
        // a thread is En/Dequeueing, other threads can read from a ConcurrentQueue because it uses snapshots,
        // which is not true of a regular Queue.
        // NOTE: We could also use the new ImmutableQueue in the Immutable Collections package on NuGet...
        private object _lock = new object(); 
        
        public ConcurrentInMemoryPersistenceStore() : this(DefaultBufferSize) { }

        public ConcurrentInMemoryPersistenceStore(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        public virtual void Save(GlimpseMetadata metadata)
        {
            Interlocked.Exchange(ref _metadata, metadata);
        }

        public virtual void Save(GlimpseRequest request)
        {
            lock (_lock)
            {
                GlimpseRequest _;
                while (_requests.Count > DefaultBufferSize)
                {
                    _requests.TryDequeue(out _);
                }
                _requests.Enqueue(request);
            }
        }

        public virtual GlimpseRequest GetByRequestId(Guid requestId)
        {
            // ConcurrentQueue's enumerator uses a snapshot of the queue, so it's thread-safe.
            return _requests.FirstOrDefault(r => r.RequestId == requestId);
        }

        public virtual TabResult GetByRequestIdAndTabKey(Guid requestId, string tabKey)
        {
            var request = _requests.FirstOrDefault(r => r.RequestId == requestId);
            if (request == null)
            {
                return null;
            }
            TabResult result;
            if (!request.TabData.TryGetValue(tabKey, out result))
            {
                return null;
            }
            return result;
        }

        public virtual IEnumerable<GlimpseRequest> GetByRequestParentId(Guid parentRequestId)
        {
            return _requests.Where(r => r.ParentRequestId.HasValue && r.ParentRequestId.Value == parentRequestId).ToList();
        }

        public virtual GlimpseMetadata GetMetadata()
        {
            return _metadata;
        }

        public virtual IEnumerable<GlimpseRequest> GetTop(int count)
        {
            return _requests.Take(count).ToList();
        }
    }
}