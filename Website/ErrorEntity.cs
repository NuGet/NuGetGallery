using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table.DataServices;

namespace NuGetGallery
{
        public class ErrorEntity : TableServiceEntity
        {
            public string SerializedError { get; set; }

            public ErrorEntity() { }
            public ErrorEntity(Error error)
                : base(string.Empty, (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19", CultureInfo.InvariantCulture))
            {
                this.SerializedError = ErrorXml.EncodeString(error);
            }
        }

        public class TableErrorLog : ErrorLog
        {
            public const string TableName = "ElmahErrors";

            private string connectionString;

            public override ErrorLogEntry GetError(string id)
            {
                return new ErrorLogEntry(
                    this, 
                    id, 
                    ErrorXml.DecodeString(
                        CloudStorageAccount.Parse(connectionString)
                                           .CreateCloudTableClient()
                                           .GetTableServiceContext()
                                           .CreateQuery<ErrorEntity>(TableName)
                                           .Where(e => e.PartitionKey == string.Empty && e.RowKey == id)
                                           .Single()
                                           .SerializedError));
            }

            public override int GetErrors(int pageIndex, int pageSize, IList errorEntryList)
            {
                var count = 0;
                var errors = CloudStorageAccount.Parse(connectionString)
                                                .CreateCloudTableClient()
                                                .GetTableServiceContext()
                                                .CreateQuery<ErrorEntity>(TableName).Where(e => e.PartitionKey == string.Empty)
                                                .Take((pageIndex + 1) * pageSize)
                                                .ToList()
                                                .Skip(pageIndex * pageSize);
                foreach (var error in errors)
                {
                    errorEntryList.Add(new ErrorLogEntry(this, error.RowKey, ErrorXml.DecodeString(error.SerializedError)));
                    count += 1;
                }
                return count;
            }

            public override string Log(Error error)
            {
                var entity = new ErrorEntity(error);
                var context = CloudStorageAccount.Parse(connectionString)
                                                 .CreateCloudTableClient()
                                                 .GetTableServiceContext();
                context.AddObject(TableName, entity);
                context.SaveChangesWithRetries();
                return entity.RowKey;
            }

            public TableErrorLog(IDictionary config)
            {
                connectionString = (string)config["connectionString"] ?? RoleEnvironment.GetConfigurationSettingValue((string)config["connectionStringName"]);
                Initialize();
            }

            public TableErrorLog(string connectionString)
            {
                this.connectionString = connectionString;
                Initialize();
            }

            void Initialize()
            {
                CloudStorageAccount.Parse(connectionString)
                                   .CreateCloudTableClient()
                                   .GetTableReference(TableName)
                                   .CreateIfNotExists();
            }
        }

}