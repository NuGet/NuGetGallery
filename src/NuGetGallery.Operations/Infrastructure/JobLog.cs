// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLog
    {
        private static JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            CheckAdditionalContent = false,
            MaxDepth = 100
        };

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Needed to add to converters list")]
        static JobLog()
        {
            _serializerSettings.Converters.Add(new LogLevelConverter());
        }

        private IList<JobLogBlob> _blobs;

        public string JobName { get; private set; }
        public IEnumerable<JobLogBlob> Blobs { get { return _blobs; } }

        public JobLog(string jobName, List<JobLogBlob> blobs)
        {
            JobName = jobName;

            // Order by descending date
            _blobs = blobs
                .OrderByDescending(b => b.ArchiveTimestamp)
                .ToList();
        }

        public IEnumerable<JobLogEntry> OrderedEntries()
        {
            foreach (var logBlob in _blobs)
            {
                // Load the blob and grab the entries
                var entries = LoadEntries(logBlob);
                foreach (var entry in entries)
                {
                    yield return entry;
                }
            }
        }

        public static IEnumerable<JobLog> LoadJobLogs(CloudStorageAccount account)
        {
            // List available blobs in "wad-joblogs" container
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference("wad-joblogs");
            var groups = container
                .ListBlobs(useFlatBlobListing: true)
                .OfType<CloudBlockBlob>()
                .Select(b => new JobLogBlob(b))
                .GroupBy(b => b.JobName);

            // Create Job Log info
            var joblogs = groups.Select(g => new JobLog(g.Key, g.ToList()));
            return joblogs;
        }

        private IEnumerable<JobLogEntry> LoadEntries(JobLogBlob logBlob)
        {
            // Download the blob to a temp file
            var temp = Path.GetTempFileName();
            try
            {
                logBlob.Blob.DownloadToFile(temp);

                // Each line is an entry! Read them in reverse though
                foreach (var line in File.ReadAllLines(temp).Reverse())
                {
                    yield return ParseEntry(line);
                }
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static JobLogEntry ParseEntry(string line)
        {
            var result = JsonConvert.DeserializeObject<JobLogEntry>(line.Trim(), _serializerSettings);
            return result;
        }
    }
}
