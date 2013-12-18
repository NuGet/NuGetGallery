using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Storage
{
    public class TableStorageHub
    {
        public static readonly string TableNamePrefix = "NG";

        public CloudTableClient Client { get; private set; }

        public TableStorageHub(CloudTableClient client)
        {
            Client = client;
        }

        public virtual AzureTable<TEntity> Table<TEntity>() where TEntity : ITableEntity, new()
        {
            return new AzureTable<TEntity>(Client, TableNamePrefix);
        }

        public virtual AzureTable<TEntity> Table<TEntity>(string name) where TEntity : ITableEntity, new()
        {
            return new AzureTable<TEntity>(Client.GetTableReference(GetTableFullName(name)));
        }

        public virtual string GetTableFullName(string tableName)
        {
            return TableNamePrefix + tableName;
        }
    }
}
