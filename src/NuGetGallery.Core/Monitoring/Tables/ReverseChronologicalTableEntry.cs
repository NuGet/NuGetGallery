using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Monitoring.Tables
{
    public abstract class ReverseChronologicalTableEntry : TableEntity
    {
        public ReverseChronologicalTableEntry() { }
        public ReverseChronologicalTableEntry(string partitionKey, DateTimeOffset timestamp)
        {
            PartitionKey = partitionKey;
            Timestamp = timestamp;

            // Convert the timestamp into something that will be in reverse chronological order
            // when sorted ASCIIbetically
            // Specifically the number of ticks from the timestamp until the end of time (as defined
            // by DateTimeOffset.MaxValue ;)), padded to 19 digits with zeros.
            RowKey = (DateTimeOffset.MaxValue.UtcTicks - timestamp.UtcTicks).ToString("d19");
        }
    }
}
