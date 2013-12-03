using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Storage
{
    public class TableStorageHub
    {
        public static readonly string TableNamePrefix = "NG";

        public CloudTableClient Client { get; private set; }

        public TableStorageHub(CloudTableClient client)
        {
            Client = client;
        }

        public AzureTable<TEntity> Table<TEntity>() where TEntity : ITableEntity
        {
            return new AzureTable<TEntity>(Client, TableNamePrefix);
        }

        public static string GetTableFullName(string tableName)
        {
            return TableNamePrefix + tableName;
        }
    }
}
