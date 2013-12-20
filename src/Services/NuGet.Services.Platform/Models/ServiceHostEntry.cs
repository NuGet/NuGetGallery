using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.Models
{
    [Table("ServiceHosts")]
    public class ServiceHostEntry : AzureTableEntity
    {
        private DatacenterName _key = null;

        [IgnoreProperty]
        public DatacenterName Datacenter
        {
            get { return _key ?? (_key = ParseKey()); }
            set { _key = value; KeyChanged(); }
        }

        public override string PartitionKey
        {
            get { return base.PartitionKey; }
            set { base.PartitionKey = value; _key = ParseKey(); }
        }

        public string Environment { get; set; }
        public int DatacenterId { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string MachineName { get; set; }
        public int InstancePort { get; set; }

        [Obsolete("For serialization only")]
        public ServiceHostEntry() { }

        public ServiceHostEntry(ServiceHostDescription description)
        {
            Datacenter = description.ServiceHostName.Datacenter;
            RowKey = description.ServiceHostName.Name.ToString();
            Timestamp = DateTimeOffset.UtcNow;

            Environment = description.ServiceHostName.Datacenter.Environment;
            DatacenterId = description.ServiceHostName.Datacenter.Id;
            Name = description.ServiceHostName.Name;
            FullName = description.ServiceHostName.ToString();
            MachineName = description.MachineName;
        }

        public static TableStorageKey GetKey(ServiceHostName serviceHostName)
        {
            return new TableStorageKey(
                serviceHostName.Datacenter.ToString().ToLowerInvariant(),
                serviceHostName.Name);
        }

        private DatacenterName ParseKey()
        {
            return DatacenterName.Parse(PartitionKey);
        }

        private void KeyChanged()
        {
            PartitionKey = Datacenter.ToString().ToLowerInvariant();
        }
    }
}
