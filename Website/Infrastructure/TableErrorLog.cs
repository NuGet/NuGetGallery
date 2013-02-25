using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Infrastructure
{
        public class ErrorEntity : ITableEntity
        {
            public string SerializedError { get; set; }

            string ITableEntity.ETag
            {
                get;
                set;
            }

            string ITableEntity.PartitionKey
            {
                get;
                set;
            }

            string ITableEntity.RowKey
            {
                get;
                set;
            }

            DateTimeOffset ITableEntity.Timestamp
            {
                get;
                set;
            }

            public long LogicalIndex
            {
                get
                {
                    // Chop off leading "Page_"
                    long page = Int64.Parse(((ITableEntity)this).PartitionKey.Substring(5), CultureInfo.InvariantCulture);
                    long offset = Int32.Parse(((ITableEntity)this).RowKey, CultureInfo.InvariantCulture);
                    return page + offset;
                }
            }

            public ErrorEntity() { }

            public ErrorEntity(Error error)
            {
                this.SerializedError = ErrorXml.EncodeString(error);
            }

            void ITableEntity.ReadEntity(IDictionary<string, EntityProperty> properties, Microsoft.WindowsAzure.Storage.OperationContext operationContext)
            {
                this.SerializedError = properties["SerializedError"].StringValue;
            }

            IDictionary<string, EntityProperty> ITableEntity.WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    { "SerializedError", EntityProperty.GeneratePropertyForString(this.SerializedError) }
                };
            }
        }

        public class TableErrorLog : ErrorLog
        {
            public const string TableName = "ElmahErrors";

            private readonly string _connectionString;
            private readonly AzureEntityList<ErrorEntity> _entityList;

            public TableErrorLog(IDictionary config)
            {
                _connectionString = (string)config["connectionString"] ?? RoleEnvironment.GetConfigurationSettingValue((string)config["connectionStringName"]);
                _entityList = new AzureEntityList<ErrorEntity>(_connectionString, TableName);
            }

            public TableErrorLog(string connectionString)
            {
                _connectionString = connectionString;
                _entityList = new AzureEntityList<ErrorEntity>(connectionString, TableName);
            }

            public TableErrorLog()
            {
                _entityList = new AzureEntityList<ErrorEntity>(_connectionString, TableName);
            }

            public override ErrorLogEntry GetError(string id)
            {
                long pos = Int64.Parse(id, CultureInfo.InvariantCulture);
                var error = _entityList[pos];
                Debug.Assert(id == pos.ToString(CultureInfo.InvariantCulture));
                return new ErrorLogEntry(this, id, ErrorXml.DecodeString(error.SerializedError));
            }

            public override int GetErrors(int pageIndex, int pageSize, IList errorEntryList)
            {
                // A little math is required since the AzureEntityList is in ascending order
                // And we want to retrieve entries in descending order
                long queryOffset = _entityList.LongCount - ((pageIndex+1) * pageSize);
                if (queryOffset < 0)
                {
                    pageSize += (int)queryOffset;
                    queryOffset = 0;
                }

                // And since that range was in ascending, flip it to descending.
                var results = _entityList.GetRange(queryOffset, pageSize).Reverse();
                foreach (var error in results)
                {
                    string id = error.LogicalIndex.ToString(CultureInfo.InvariantCulture);
                    errorEntryList.Add(new ErrorLogEntry(this, id, ErrorXml.DecodeString(error.SerializedError)));
                }

                return _entityList.Count;
            }

            public override string Log(Error error)
            {
                var entity = new ErrorEntity(error);
                long pos = _entityList.Add(entity);
                return pos.ToString(CultureInfo.InvariantCulture);
            }
        }
}