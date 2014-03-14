using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace NuGetGallery
{
    public class MessageQueue
    {
        internal static ConcurrentDictionary<string, ConcurrentQueue<object>> _queues = new ConcurrentDictionary<string, ConcurrentQueue<object>>();

        public static int MaxPerQueue { get; private set; }
        public static bool Enabled { get; private set; }

        // Must be enabled to ensure someone is actually there to listen :)
        public static void Enable(int maxPerQueue)
        {
            Enabled = true;
            MaxPerQueue = maxPerQueue;
        }

        public static void Enqueue<T>(string queueName, T payload)
        {
            if (Enabled)
            {
                var queue = _queues.GetOrAdd(queueName, _ => new ConcurrentQueue<object>());
                
                object __;
                while (queue.Count > (MaxPerQueue - 1) && queue.TryDequeue(out __)) { }
                queue.Enqueue(payload);
            }
        }

        public static IEnumerable<T> GetBatch<T>(string queueName)
        {
            if (!Enabled)
            {
                throw new InvalidOperationException("Items cannot be read from the EventQueue unless it is enabled!");
            }

            ConcurrentQueue<object> queue;
            if (!_queues.TryGetValue(queueName, out queue))
            {
                yield break;
            }
            
            // Dump the queue
            object item;
            while (queue.TryDequeue(out item))
            {
                yield return (T)item;
            }
        }

        internal static IDictionary<string, int> GetQueueStats()
        {
            return _queues.ToDictionary(p => p.Key, p => p.Value.Count);
        }
    }
}