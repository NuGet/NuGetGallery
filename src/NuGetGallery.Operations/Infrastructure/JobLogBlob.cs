using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLogBlob
    {
        private static readonly Regex BlobNameParser = new Regex(@"^(?<deployment>[^/]*)/(?<role>[^/]*)/(?<instance>[^/]*)/(?<job>[^\.]*)(\.(?<timestamp>\d{4}\-\d{2}\-\d{2}))?(\.(?<seq>\d+))?\.log\.json$");

        // Bob Lobwlaw's Job Log Blob!
        public CloudBlockBlob Blob { get; private set; }

        public string JobName { get; private set; }
        public DateTime? ArchiveTimestamp { get; private set; }
        
        public JobLogBlob(CloudBlockBlob blob)
        {
            Blob = blob;

            // Parse the name
            var parsed = BlobNameParser.Match(blob.Name);
            if (!parsed.Success)
            {
                throw new ArgumentException("Job Log Blob name is invalid Bob! Expected [jobname].[yyyy-MM-dd].json or [jobname].json. Got: " + blob.Name, "blob");
            }

            // Grab the chunks we care about
            JobName = parsed.Groups["job"].Value;
            if (parsed.Groups["timestamp"].Success)
            {
                ArchiveTimestamp = DateTime.ParseExact(parsed.Groups["timestamp"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }
    }
}
