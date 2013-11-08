using System;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;

namespace NuGetGallery.Monitoring.Tables
{
    public class MonitoringHub
    {
        public static readonly string TableNamePrefix = "NGM";

        protected CloudTableClient Tables { get; private set; }
        protected CloudBlobClient Blobs { get; private set; }
        protected CloudQueueClient Queues { get; private set; }

        private Dictionary<Type, string> _tableNameMap = new Dictionary<Type, string>();
        private Dictionary<string, CloudBlobContainer> _containerMap = new Dictionary<string, CloudBlobContainer>();
        
        public CloudStorageAccount DiagnosticsStorage { get; private set; }
        protected string StorageConnectionString { get; private set; }

        public MonitoringHub(string storageConnectionString)
        {
            DiagnosticsStorage = CloudStorageAccount.Parse(storageConnectionString);

            StorageConnectionString = storageConnectionString;

            Tables = DiagnosticsStorage.CreateCloudTableClient();
            Blobs = DiagnosticsStorage.CreateCloudBlobClient();
            Queues = DiagnosticsStorage.CreateCloudQueueClient();
        }

        public string GetTableFullName(string tableName)
        {
            return TableNamePrefix + tableName;
        }

        public MonitoringTable<TEntity> Table<TEntity>() where TEntity : ITableEntity
        {
            string tableName;
            if (!_tableNameMap.TryGetValue(typeof(TEntity), out tableName))
            {
                tableName = GetTableName(typeof(TEntity));
                _tableNameMap[typeof(TEntity)] = tableName;
            }
            var table = Tables.GetTableReference(tableName);
            return new MonitoringTable<TEntity>(table);
        }

        public Task<CloudBlockBlob> UploadBlob(string sourceFileName, string containerName, string path)
        {
            CloudBlobContainer container;
            if (!_containerMap.TryGetValue(containerName, out container))
            {
                _containerMap[containerName] = 
                    container = 
                        Blobs.GetContainerReference(containerName);
            }
            return container.SafeExecute(async ct =>
            {
                var blob = ct.GetBlockBlobReference(path);
                await blob.UploadFromFileAsync(sourceFileName, FileMode.Open);
                return blob;
            });
        }

        public virtual Task Start()
        {
            // Starts monitoring tasks.
            return Task.FromResult<object>(null);
        }

        private string GetTableName(Type typ)
        {
            string name = typ.Name;
            TableAttribute attr = typ.GetCustomAttribute<TableAttribute>();
            if (attr != null)
            {
                name = attr.Name;
            }
            else
            {
                if (name.EndsWith("Entry"))
                {
                    name = name.Substring(0, name.Length - 5);
                }
            }
            return GetTableFullName(name);
        }
    }
}
