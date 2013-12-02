using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Jobs.Monitoring
{
    public class InstancesEntry : TableEntity
    {
        public string Name
        {
            get { return RowKey; }
            set { RowKey = value; }
        }

        [Obsolete("For serialization only")]
        public InstancesEntry() { }

        public InstancesEntry(string name)
            : base(partitionKey: String.Empty, rowKey: name)
        {
            Timestamp = DateTimeOffset.UtcNow;
        }

        public string MachineName { get; set; }

        public static InstancesEntry ForCurrentMachine(string instanceName)
        {
            return new InstancesEntry(instanceName)
            {
                MachineName = Environment.MachineName
            };
        } 
    }
}
