// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
        private static readonly Regex BlobNameParser = new Regex(@"^(?<deployment>[^/]*)/(?<role>[^/]*)/(?<instance>[^/]*)/(?<job>[^\.]*)\.(?<timestamp>\d{4}\-\d{2}\-\d{2}(\-\d{4})?)\.log\.json$");
        private const string HourTimeStamp = "yyyy-MM-dd-HHmm";
        private const string DayTimeStamp = "yyyy-MM-dd";

        // Bob Lobwlaw's Job Log Blob!
        public CloudBlockBlob Blob { get; private set; }

        public string JobName { get; private set; }
        public DateTime ArchiveTimestamp { get; private set; }
        
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

            string format = DayTimeStamp;
            if (parsed.Groups[1].Success)
            {
                // Has an hour portion!
                format = HourTimeStamp;
            }

            ArchiveTimestamp = DateTime.ParseExact(
                parsed.Groups["timestamp"].Value, 
                format, 
                CultureInfo.InvariantCulture);
        }
    }
}
