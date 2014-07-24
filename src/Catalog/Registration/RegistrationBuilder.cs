using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationBuilder
    {
        SortedList<string, Entry> _entries = new SortedList<string, Entry>(StringComparer.InvariantCultureIgnoreCase);
        Storage _storage;
        int _segmentSize;
        string _name;

        JObject _segmentFrame;
        JObject _segmentIndexFrame;

        bool _verbose;
 
        public RegistrationBuilder(Storage storage, string name, int segmentSize, bool verbose = false)
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
        public void Add(Entry entry, Func<Entry, string> key)
        {
            _entries.Add(key(entry), entry);
        }

        public void Add(Entry entry)
        {
            Add(entry, (e) => e.Id);
        }

        public async Task Commit()
        {
            Uri segmentIndexUri = _storage.ResolveUri(_name + "segment_index.json");
            SegmentIndex segmentIndex = new SegmentIndex(segmentIndexUri);

            SortedList<string, Entry> batch = new SortedList<string, Entry>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var item in _entries)
            {
                batch.Add(item.Key, item.Value);
                if (batch.Count == _segmentSize)
                {
                    await AddSegment(segmentIndex, batch);
                    batch.Clear();
                }
            }
            await AddSegment(segmentIndex, batch);

            IGraph graph = segmentIndex.ToGraph();

            await _storage.Save(segmentIndexUri, new StringStorageContent(Utils.CreateJson(graph, _segmentIndexFrame), "application/json"));

            if (_verbose)
            {
                Console.WriteLine(segmentIndexUri);
            }
        }

        async Task AddSegment(SegmentIndex segmentIndex, SortedList<string, Entry> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            Uri segmentUri = _storage.ResolveUri(_name + segmentIndex.GetNextSegmentName());

            Segment segment = new Segment(segmentUri);

            foreach (var item in batch)
            {
                Segment.SegmentEntry segmentEntry = new Segment.SegmentEntry();
                segmentEntry.Uri = item.Value.Uri;
                segmentEntry.Id = item.Value.Id;
                segmentEntry.Version = item.Value.Version;
                segmentEntry.Description = item.Value.Description;

                segment.Entries.Add(segmentEntry);
            }

            IGraph graph = segment.ToGraph();

            await _storage.Save(segmentUri, new StringStorageContent(Utils.CreateJson(graph, _segmentFrame), "application/json"));

            if (_verbose)
            {
                Console.WriteLine(segmentUri);
            }

            SegmentIndex.SegmentSummary segmentSummary = new SegmentIndex.SegmentSummary();
            segmentSummary.Uri = segmentUri;
            segmentSummary.Lowest = batch.First().Key;
            segmentSummary.Highest = batch.Last().Key;
            segmentSummary.Count = batch.Count;

            segmentIndex.Segments.Add(segmentSummary);
        }
    }
}
