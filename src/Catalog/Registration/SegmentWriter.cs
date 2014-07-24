using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class SegmentWriter
    {
        SortedList<string, SegmentEntry> _entries = new SortedList<string, SegmentEntry>();
        IDictionary<string, SegmentEntry> _data = new Dictionary<string, SegmentEntry>();

        Storage _storage;
        int _segmentSize;
        string _name;

        JObject _segmentFrame;
        JObject _segmentIndexFrame;

        bool _verbose;
 
        public SegmentWriter(Storage storage, string name, int segmentSize, bool verbose = false)
        {
            _segmentSize = segmentSize;
            _storage = storage;
            _name = name.Trim('/') + '/';

            _segmentFrame = JObject.Parse(Utils.GetResource("context.Registration.json"));
            _segmentFrame["@type"] = "http://schema.nuget.org/catalog#Segment";
            _segmentIndexFrame = JObject.Parse(Utils.GetResource("context.Registration.json"));
            _segmentIndexFrame["@type"] = "http://schema.nuget.org/catalog#SegmentIndex";

            _verbose = verbose;
        }
        
        public void Add(SegmentEntry entry)
        {
            _entries.Add(entry.Key, entry);
            _data.Add(entry.Key, entry);
        }
        
        public async Task Commit()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            DateTime commitTimeStamp = DateTime.UtcNow;
            Guid commitId = Guid.NewGuid();

            // load or create the segment index

            Uri segmentIndexUri = _storage.ResolveUri(_name + "segment_index.json");

            string segmentIndexContent = await _storage.LoadString(segmentIndexUri);

            SegmentIndex segmentIndex;
            if (segmentIndexContent != null)
            {
                segmentIndex = new SegmentIndex(Utils.CreateGraph(segmentIndexContent));
            }
            else
            {
                segmentIndex = new SegmentIndex(segmentIndexUri);
            }

            // create in-memory buckets following any existing segmented state

            SortedDictionary<string, SegmentBucket> buckets = new SortedDictionary<string, SegmentBucket>();

            CreateInitialBuckets(buckets, segmentIndex.Segments);

            // fill the existing buckets with the new data

            FillBuckets(buckets, _entries.Values);

            // drop any buckets we didn't have data for

            RemoveEmptyBuckets(buckets);

            // if the commit can fit in one segment then the rewriting can be optimized

            SegmentIndex.SegmentSummary existingSegmentSummary;
            if (TryCommitScopedToSingleSegment(buckets, segmentIndex.Segments, out existingSegmentSummary))
            {
                SegmentBucket segmentBucket = buckets.Values.First();

                // load the existing segment and merge the data into teh bucket

                string segmentContent = await _storage.LoadString(existingSegmentSummary.Uri);

                Segment oldSegment = new Segment(Utils.CreateGraph(segmentContent));

                foreach (Segment.SegmentEntry segmentEntry in oldSegment.Entries)
                {
                    if (!segmentBucket.Entries.ContainsKey(segmentEntry.Key))
                    {
                        // items with the same key will be replaced with new data
                        segmentBucket.Entries.Add(segmentEntry.Key, new LoadedSegmentEntry(segmentEntry));
                    }
                }

                // create the new segment from the data in the bucket

                Segment newSegment = MakeSegmentFromBucket(existingSegmentSummary.Uri, buckets.Values.First());

                // save will overwrite the odl segment with the new

                await _storage.Save(newSegment.Uri, new StringStorageContent(Utils.CreateJson(newSegment.ToGraph(), _segmentFrame), "application/json"));
            }
            else
            {
                HashSet<Uri> segmentsToDelete = new HashSet<Uri>();

                // process the buckets, potentially by splitting them up to create more segments, for transactional reasons this process always renames segments

                await ProcessBuckets(buckets, segmentIndex, segmentsToDelete);

                // clean any old SegmentSummary items out of the SegmentIndex

                CleanUpSegmentIndex(segmentIndex, segmentsToDelete);

                // finally save the index - saving the index confirms the transaction because it will be saved with new segment names

                await _storage.Save(segmentIndex.Uri, new StringStorageContent(Utils.CreateJson(segmentIndex.ToGraph(), _segmentIndexFrame), "application/json"));

                // rewriting segments will leave us with old segments to delete - this is clean up - at this point the transaction is committed

                foreach (Uri segmentUri in segmentsToDelete)
                {
                    await _storage.Delete(segmentUri);
                }
            }

            _entries.Clear();
            _data.Clear();
        }

        void CreateInitialBuckets(SortedDictionary<string, SegmentBucket> buckets, IList<SegmentIndex.SegmentSummary> existingSegments)
        {
            foreach (SegmentIndex.SegmentSummary segmentSummary in existingSegments)
            {
                buckets.Add(segmentSummary.Lowest, new SegmentBucket 
                { 
                    Count = segmentSummary.Count,
                    SegmentUri = segmentSummary.Uri
                });
            }
        }

        void FillBuckets(SortedDictionary<string, SegmentBucket> buckets, IList<SegmentEntry> values)
        {
            foreach (SegmentEntry entry in values)
            {
                SegmentBucket segmentBucket = GetSegmentBucket(buckets, entry.Key);
                segmentBucket.Entries.Add(entry.Key, entry);
            }
        }

        SegmentBucket GetSegmentBucket(SortedDictionary<string, SegmentBucket> buckets, string key)
        {
            if (buckets.Count == 0)
            {
                SegmentBucket newBucket = new SegmentBucket();
                buckets.Add(key, newBucket);
                return newBucket;
            }

            string bucketKey = buckets.First().Key;

            foreach (string currentBucketKey in buckets.Keys)
            {
                if (currentBucketKey.CompareTo(key) > 0)
                {
                    break;
                }
                bucketKey = currentBucketKey;
            }

            return buckets[bucketKey];
        }

        void RemoveEmptyBuckets(SortedDictionary<string, SegmentBucket> buckets)
        {
            HashSet<string> unused = new HashSet<string>();

            foreach (KeyValuePair<string, SegmentBucket> bucket in buckets)
            {
                if (bucket.Value.Entries.Count == 0)
                {
                    unused.Add(bucket.Key);
                }
            }

            foreach (string bucketKey in unused)
            {
                buckets.Remove(bucketKey);
            }
        }

        bool TryCommitScopedToSingleSegment(SortedDictionary<string, SegmentBucket> buckets, IList<SegmentIndex.SegmentSummary> existingSegments, out SegmentIndex.SegmentSummary existingSegmentSummary)
        {
            existingSegmentSummary = null;

            if (buckets.Count > 1)
            {
                return false;
            }

            if (buckets.Count == 0)
            {
                //  it shouldn't be possible to get here
                return false;
            }

            string bucketKey = buckets.First().Key;
            int bucketCount = buckets.First().Value.Entries.Count;

            // check whether this bucket corresponds to an existing segment
            foreach (SegmentIndex.SegmentSummary segment in existingSegments)
            {
                if (segment.Lowest == bucketKey && segment.Count + bucketCount < _segmentSize)
                {
                    existingSegmentSummary = segment;
                    return true;
                }
            }
            return false;
        }

        Segment MakeSegmentFromBucket(Uri uri, SegmentBucket bucket)
        {
            Segment segment = new Segment(uri);

            foreach (SegmentEntry segmentEntry in bucket.Entries.Values)
            {
                Uri segmentEntryUri = new Uri(segment.Uri.ToString() + "#" + segmentEntry.Key);
                IGraph graph = segmentEntry.GetSegmentContent(segmentEntryUri);
                Segment.SegmentEntry newSegmentEntry = new Segment.SegmentEntry(graph)
                { 
                    Uri = segmentEntryUri,
                    Key = segmentEntry.Key
                };
                segment.Entries.Add(newSegmentEntry);
            }

            return segment;
        }

        async Task ProcessBuckets(SortedDictionary<string, SegmentBucket> buckets, SegmentIndex segmentIndex, HashSet<Uri> segmentsToDelete)
        {
            SortedDictionary<string, SegmentBucket> newBuckets = new SortedDictionary<string, SegmentBucket>();

            foreach (KeyValuePair<string, SegmentBucket> bucket in buckets)
            {
                if (bucket.Value.SegmentUri != null)
                {
                    string segmentContent = await _storage.LoadString(bucket.Value.SegmentUri);

                    Segment oldSegment = new Segment(Utils.CreateGraph(segmentContent));

                    foreach (Segment.SegmentEntry segmentEntry in oldSegment.Entries)
                    {
                        if (!bucket.Value.Entries.ContainsKey(segmentEntry.Key))
                        {
                            // items with the same key will be replaced with new data
                            bucket.Value.Entries.Add(segmentEntry.Key, new LoadedSegmentEntry(segmentEntry));
                        }
                    }
                }

                int newSegmentCount = bucket.Value.Entries.Count / _segmentSize;

                if (newSegmentCount > 1)
                {
                    if (newSegmentCount * _segmentSize < bucket.Value.Entries.Count)
                    {
                        newSegmentCount++;
                    }

                    SegmentBucket[] subBuckets = new SegmentBucket[newSegmentCount];

                    for (int i = 0; i < subBuckets.Length; i++)
                    {
                        subBuckets[i] = new SegmentBucket();
                    }

                    int j = 0;
                    foreach (KeyValuePair<string, SegmentEntry> entry in bucket.Value.Entries)
                    {
                        int subBucketIndex = j++ / _segmentSize;

                        subBuckets[subBucketIndex].Entries.Add(entry.Key, entry.Value);
                    }

                    foreach (SegmentBucket subBucket in subBuckets)
                    {
                        Uri segmentUri = _storage.ResolveUri(_name + segmentIndex.GetNextSegmentName());

                        Segment newSegment = MakeSegmentFromBucket(segmentUri, subBucket);

                        await _storage.Save(newSegment.Uri, new StringStorageContent(Utils.CreateJson(newSegment.ToGraph(), _segmentFrame), "application/json"));

                        SegmentIndex.SegmentSummary segmentSummary = new SegmentIndex.SegmentSummary
                        {
                            Count = newSegment.Entries.Count,
                            Uri = newSegment.Uri,
                            Lowest = newSegment.Entries.First().Key
                        };

                        segmentIndex.Segments.Add(segmentSummary);
                    }
                }
                else
                {
                    Uri segmentUri = _storage.ResolveUri(_name + segmentIndex.GetNextSegmentName());

                    Segment newSegment = MakeSegmentFromBucket(segmentUri, bucket.Value);

                    await _storage.Save(newSegment.Uri, new StringStorageContent(Utils.CreateJson(newSegment.ToGraph(), _segmentFrame), "application/json"));

                    SegmentIndex.SegmentSummary segmentSummary = new SegmentIndex.SegmentSummary
                    {
                        Count = newSegment.Entries.Count,
                        Uri = newSegment.Uri,
                        Lowest = newSegment.Entries.First().Key
                    };

                    segmentIndex.Segments.Add(segmentSummary);
                }

                if (bucket.Value.SegmentUri != null)
                {
                    segmentsToDelete.Add(bucket.Value.SegmentUri);
                }
            }
        }

        void CleanUpSegmentIndex(SegmentIndex segmentIndex, HashSet<Uri> segmentsToDelete)
        {
            IList<SegmentIndex.SegmentSummary> segmentSummaryToDelete = new List<SegmentIndex.SegmentSummary>();

            foreach (SegmentIndex.SegmentSummary segmentSummary in segmentIndex.Segments)
            {
                if (segmentsToDelete.Contains(segmentSummary.Uri))
                {
                    segmentSummaryToDelete.Add(segmentSummary);
                }
            }

            foreach (SegmentIndex.SegmentSummary segmentSummary in segmentSummaryToDelete)
            {
                segmentIndex.Segments.Remove(segmentSummary);
            }
        }

        class LoadedSegmentEntry : SegmentEntry
        {
            IGraph _graph;
            string _key;
            Uri _originalUri;

            public LoadedSegmentEntry(Segment.SegmentEntry segmentEntry)
            {
                _originalUri = segmentEntry.Uri; 
                _key = segmentEntry.Key;
                _graph = segmentEntry.ToGraph();
            }

            public override string Key
            {
                get { return _key; }
            }

            public override IGraph GetSegmentContent(Uri uri)
            {
                if (uri == _originalUri)
                {
                    return _graph;
                }
                else
                {
                    INode newSubject = _graph.CreateUriNode(uri);

                    IList<Triple> triplesToRetract = new List<Triple>();

                    foreach (Triple triple in _graph.GetTriplesWithSubject(_graph.CreateUriNode(_originalUri)))
                    {
                        _graph.Assert(newSubject, triple.Predicate, triple.Object);
                        triplesToRetract.Add(triple);
                    }

                    foreach (Triple triple in triplesToRetract)
                    {
                        _graph.Retract(triple);
                    }

                    return _graph;
                }
            }
        }
    }
}
